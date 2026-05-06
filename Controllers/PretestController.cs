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
        ["p2"] = "50",
        ["p3"] = "25",
        ["p4"] = "2\n4\n6\n8\n10",
        ["p5"] = "3",
    };

    // Secondary tests: replace the known array literal with different values and re-run.
    // A hardcoded Console.WriteLine will produce the wrong output for the new array.
    private static readonly Dictionary<string, (string ArrayRegex, string NewArray, string Expected)> SecondaryTests = new()
    {
        ["p1"] = (@"\{\s*85\s*,\s*92\s*,\s*78\s*,\s*95\s*,\s*88\s*\}", "{ 10, 20, 30, 40, 50 }", "30"),
        ["p3"] = (@"\{\s*3\s*,\s*7\s*,\s*2\s*,\s*9\s*,\s*4\s*\}",      "{ 1, 2, 3, 4, 5 }",        "15"),
        ["p4"] = (@"\{\s*1\s*,\s*2\s*,\s*3\s*,\s*4\s*,\s*5\s*\}",      "{ 3, 6, 9 }",              "6\n12\n18"),
        ["p5"] = (@"\{\s*4\s*,\s*7\s*,\s*2\s*,\s*9\s*,\s*1\s*,\s*8\s*,\s*3\s*\}", "{ 1, 2, 3, 4, 5 }", "0"),
    };

    // p2 can't use array-swap (hardcoded index would break with a shorter array),
    // so require that the code actually contains an array access expression.
    private static readonly Dictionary<string, string> RequiredPatterns = new()
    {
        ["p2"] = @"arr\[",
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
                    diagnostic.IsCorrect = false;
                    diagnostic.Category  = DiagnosticCategory.GenericLogicError;
                    diagnostic.Message   = "Your code could not be executed. Check for infinite loops or unsupported operations.";
                }
                else if (Normalize(actualOutput) != Normalize(expected))
                {
                    diagnostic.IsCorrect = false;
                    diagnostic.Category  = DiagnosticCategory.GenericLogicError;
                    diagnostic.Message   = string.IsNullOrWhiteSpace(actualOutput)
                        ? "Your code produced no output. Make sure you have a Console.WriteLine with the correct value."
                        : "Your output does not match the expected result. Review your logic.";
                }
                else
                {
                    // Stage 3 — anti-hardcode: required pattern check
                    if (RequiredPatterns.TryGetValue(request.ProblemId!, out var pattern)
                        && !Regex.IsMatch(request.SourceCode, pattern))
                    {
                        diagnostic.IsCorrect = false;
                        diagnostic.Category  = DiagnosticCategory.GenericLogicError;
                        diagnostic.Message   = "Make sure your solution uses the array — not just a hardcoded value.";
                    }
                    // Stage 3 — anti-hardcode: secondary execution with swapped array values
                    else if (SecondaryTests.TryGetValue(request.ProblemId!, out var sec))
                    {
                        var altCode = Regex.Replace(request.SourceCode, sec.ArrayRegex, sec.NewArray);
                        if (altCode != request.SourceCode) // substitution succeeded
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
            s.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
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

            var sb     = new StringBuilder();
            var writer = new StringWriter(sb);

            lock (ConsoleLock)
            {
                var saved = Console.Out;
                Console.SetOut(writer);
                try   { entry.Invoke(null, argv); }
                finally { Console.SetOut(saved); }
            }

            return sb.ToString().Trim();
        }
        catch
        {
            return null;
        }
    }
}

public class PretestCompileRequest
{
    public string  SourceCode { get; set; } = "";
    public string  SkillType  { get; set; } = "ArrayInitialization";
    public string? ProblemId  { get; set; }
}
