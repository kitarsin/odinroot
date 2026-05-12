using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ODIN.Api.Models.Enums;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Controllers;

[ApiController]
[Route("api/pretest")]
public class PretestController : ControllerBase
{
    private readonly IDiagnosticEngine _diagnosticEngine;

    // Expected primary output per problem
    private static readonly Dictionary<string, string> ExpectedOutputs = new()
    {
        ["p1"] = "78",
        ["p2"] = "20",
        ["p3"] = "50\n10",
        ["p4"] = "18",
        ["p5"] = "4",
    };

    // Secondary tests: replace the known array literal with different values and re-run.
    // If the student's code no longer contains the expected array (deleted it), that also fails.
    // p2 uses a same-length replacement so arr[4] still works; it only catches Console.WriteLine(50) bypasses.
    private static readonly Dictionary<string, (string ArrayRegex, string NewArray, string Expected)> SecondaryTests = new()
    {
        ["p1"] = (@"\{\s*85\s*,\s*92\s*,\s*78\s*,\s*95\s*,\s*88\s*\}",                                         "{ 10, 20, 30, 40, 50 }",    "30"),
        ["p2"] = (@"\{\s*3\s*,\s*8\s*,\s*2\s*,\s*9\s*,\s*4\s*,\s*7\s*,\s*6\s*\}",                              "{ 1, 2, 3, 4, 5 }",         "6"),
        ["p3"] = (@"\{\s*10\s*,\s*20\s*,\s*30\s*,\s*40\s*,\s*50\s*\}",                                         "{ 99, 88, 77 }",            "77\n99"),
        ["p4"] = (@"\{\s*14\s*,\s*3\s*,\s*27\s*,\s*9\s*,\s*27\s*,\s*18\s*,\s*5\s*\}",                         "{ 10, 5, 20, 20, 15 }",     "15"),
        ["p5"] = (@"\{\s*3\s*,\s*1\s*,\s*4\s*,\s*5\s*,\s*9\s*,\s*2\s*,\s*6\s*,\s*5\s*,\s*3\s*,\s*7\s*,\s*8\s*\}", "{ 10, 20, 30, 5, 6 }",      "3"),
    };

    // Cached metadata references — built once from TRUSTED_PLATFORM_ASSEMBLIES
    private static readonly Lazy<IReadOnlyList<MetadataReference>> PlatformRefs = new(() =>
    {
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "";
        return trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(System.IO.File.Exists)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
    });

    // Guard concurrent Console.SetOut calls
    private static readonly object ConsoleLock = new();

    public PretestController(IDiagnosticEngine diagnosticEngine)
    {
        _diagnosticEngine = diagnosticEngine;
    }

    [HttpPost("compile")]
    public async Task<IActionResult> Compile([FromBody] PretestCompileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceCode))
            return BadRequest(new { error = "Source code is required" });

        if (!Enum.TryParse<SkillType>(request.SkillType, out var skillType))
            skillType = SkillType.ArrayInitialization;

        // Stage 1 — static analysis
        var diagnostic = _diagnosticEngine.Diagnose(request.SourceCode, skillType);

        string? actualOutput = null;

        if (!diagnostic.CompilerDiagnostics.Any())
        {
            if (!ExpectedOutputs.TryGetValue(request.ProblemId ?? "", out var expected))
            {
                diagnostic.IsCorrect = false;
                diagnostic.Category  = DiagnosticCategory.GenericLogicError;
                diagnostic.Message   = "Unknown problem identifier.";
            }
            else
            {
                // Stage 2 — execute and compare primary output
                actualOutput = await ExecuteCodeAsync(request.SourceCode);

                if (actualOutput == null)
                {
                    if (diagnostic.IsCorrect)
                    {
                        diagnostic.IsCorrect = false;
                        diagnostic.Category  = DiagnosticCategory.GenericLogicError;
                        diagnostic.Message   = "Your code could not be executed. Check for infinite loops, excessive output, or unsupported operations.";
                    }
                }
                else if (Normalize(actualOutput) != Normalize(expected))
                {
                    diagnostic.IsCorrect = false;
                    diagnostic.Category  = DiagnosticCategory.GenericLogicError;
                    diagnostic.Message   = string.IsNullOrWhiteSpace(actualOutput)
                        ? "Your code produced no output. Make sure you have a Console.WriteLine with the correct value."
                        : $"Your output ('{actualOutput.Trim()}') does not match the expected result. Review your logic.";
                }
                else if (SecondaryTests.TryGetValue(request.ProblemId!, out var sec))
                {
                    // Stage 3 — anti-hardcode: swap the array literal and re-run.
                    // If the array literal is gone (student deleted it), that also fails —
                    // correct solutions must keep the required array in the code.
                    var altCode = Regex.Replace(request.SourceCode, sec.ArrayRegex, sec.NewArray);
                    if (altCode == request.SourceCode)
                    {
                        diagnostic.IsCorrect = false;
                        diagnostic.Category  = DiagnosticCategory.GenericLogicError;
                        diagnostic.Message   = "Make sure your code uses the provided array — do not remove the array declaration.";
                    }
                    else
                    {
                        var altOutput = await ExecuteCodeAsync(altCode);
                        if (altOutput == null || Normalize(altOutput) != Normalize(sec.Expected))
                        {
                            diagnostic.IsCorrect = false;
                            diagnostic.Category  = DiagnosticCategory.GenericLogicError;
                            diagnostic.Message   = "Your solution appears to use a hardcoded value. Make sure your code actually processes the array — it should work for any valid input.";
                        }
                    }
                }
            }
        }

        return Ok(new
        {
            isCorrect           = diagnostic.IsCorrect,
            diagnosticCategory  = diagnostic.Category.ToString(),
            diagnosticMessage   = diagnostic.Message,
            actualOutput,
            compilerDiagnostics = diagnostic.CompilerDiagnostics.Select(d => new
            {
                id       = d.Id,
                severity = d.Severity,
                message  = d.Message,
                line     = d.Line,
                column   = d.Column,
            }),
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Normalize(string s) =>
        string.Join('\n',
            s.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
             .Select(l => l.Trim())
             .Where(l => l.Length > 0));

    private static string WrapForExecution(string code) => $$"""
        using System;
        using System.Linq;
        using System.Collections.Generic;

        class __Runner
        {
            static void Main(string[] args)
            {
                {{code}}
            }
        }
        """;

    private static async Task<string?> ExecuteCodeAsync(string sourceCode)
    {
        try
        {
            return await Task.Run(() => RunCode(sourceCode))
                             .WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            return null;
        }
    }

    private static string? RunCode(string sourceCode)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(WrapForExecution(sourceCode));

            var compilation = CSharpCompilation.Create(
                assemblyName: $"Pretest_{Guid.NewGuid():N}",
                syntaxTrees: [tree],
                references: PlatformRefs.Value,
                options: new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            using var ms   = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
                return null;

            ms.Seek(0, SeekOrigin.Begin);
            var asm   = Assembly.Load(ms.ToArray());
            var entry = asm.EntryPoint ?? throw new InvalidOperationException("No entry point found.");
            var argv  = entry.GetParameters().Length == 0
                ? null
                : new object[] { Array.Empty<string>() };

            var writer = new LimitedStringWriter();

            lock (ConsoleLock)
            {
                var saved = Console.Out;
                Console.SetOut(writer);
                try   { entry.Invoke(null, argv); }
                finally { Console.SetOut(saved); }
            }

            return writer.Result.Trim();
        }
        catch
        {
            return null;
        }
    }

    // Caps output at 4 KB. Any correct pretest solution outputs at most a few lines.
    // Exceeding the limit throws, which causes RunCode to return null → execution failure.
    private sealed class LimitedStringWriter : TextWriter
    {
        private readonly StringBuilder _sb = new();
        private int _total;
        private const int Limit = 4_096;

        public override Encoding Encoding => Encoding.UTF8;
        public string Result => _sb.ToString();

        private void Guard(int n)
        {
            if (_total + n > Limit)
                throw new InvalidOperationException("Output size limit exceeded.");
        }

        public override void Write(char value)
            { Guard(1); _sb.Append(value); _total++; }

        public override void Write(string? value)
            { if (value is null) return; Guard(value.Length); _sb.Append(value); _total += value.Length; }

        public override void Write(char[] buffer, int index, int count)
            { Guard(count); _sb.Append(buffer, index, count); _total += count; }
    }
}

public class PretestCompileRequest
{
    public string  SourceCode { get; set; } = "";
    public string  SkillType  { get; set; } = "ArrayInitialization";
    public string? ProblemId  { get; set; }
}
