using System.Reflection;
using System.Text;
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

    // Expected output per problem — normalized (trimmed lines, joined with \n)
    private static readonly Dictionary<string, string> ExpectedOutputs = new()
    {
        ["p1"] = "78",
        ["p2"] = "50",
        ["p3"] = "25",
        ["p4"] = "2\n4\n6\n8\n10",
        ["p5"] = "3",
    };

    // Lock so concurrent Console.SetOut calls don't stomp each other
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

        // Stage 1: static analysis (syntax errors, known array bugs)
        var diagnostic = _diagnosticEngine.Diagnose(request.SourceCode, skillType);

        string? actualOutput = null;

        // Stage 2: execute and validate output only when syntax is clean
        if (!diagnostic.CompilerDiagnostics.Any())
        {
            actualOutput = await ExecuteCodeAsync(request.SourceCode);

            if (actualOutput == null)
            {
                diagnostic.IsCorrect = false;
                diagnostic.Category  = DiagnosticCategory.GenericLogicError;
                diagnostic.Message   = "Code could not be executed. Check for infinite loops or unsupported operations.";
            }
            else if (ExpectedOutputs.TryGetValue(request.ProblemId ?? "", out var expected))
            {
                var normalActual   = Normalize(actualOutput);
                var normalExpected = Normalize(expected);

                if (normalActual != normalExpected)
                {
                    diagnostic.IsCorrect = false;
                    diagnostic.Category  = DiagnosticCategory.GenericLogicError;
                    diagnostic.Message   = string.IsNullOrWhiteSpace(actualOutput)
                        ? "Your code produced no output. Make sure you have a Console.WriteLine with the correct value."
                        : "Your output does not match the expected result. Review your logic.";
                }
                // else: IsCorrect stays true from static analysis
            }
        }

        return Ok(new
        {
            isCorrect          = diagnostic.IsCorrect,
            diagnosticCategory = diagnostic.Category.ToString(),
            diagnosticMessage  = diagnostic.Message,
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

    /// Trim each line, drop blanks, join with \n for comparison.
    private static string Normalize(string s) =>
        string.Join('\n',
            s.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
             .Select(l => l.Trim())
             .Where(l => l.Length > 0));

    /// Wrap student code in a minimal executable scaffold.
    private static string WrapForExecution(string code) => $@"
using System;
using System.Linq;
using System.Collections.Generic;

class __Runner {{
    static void Main(string[] args) {{
        {code}
    }}
}}
";

    /// Compile and run student code, returning stdout or null on failure/timeout.
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

            var refs = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

            var compilation = CSharpCompilation.Create(
                $"Pretest_{Guid.NewGuid():N}",
                [tree],
                refs,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            using var ms = new MemoryStream();
            if (!compilation.Emit(ms).Success) return null;

            ms.Seek(0, SeekOrigin.Begin);
            var asm   = Assembly.Load(ms.ToArray());
            var entry = asm.EntryPoint ?? throw new InvalidOperationException("No entry point");
            var argv  = entry.GetParameters().Length == 0 ? null : new object[] { Array.Empty<string>() };

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
