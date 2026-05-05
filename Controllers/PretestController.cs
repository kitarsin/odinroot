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

    // Normalized expected outputs per problem (each line trimmed, joined with \n)
    private static readonly Dictionary<string, string> ExpectedOutputs = new()
    {
        ["p1"] = "78",
        ["p2"] = "50",
        ["p3"] = "25",
        ["p4"] = "2\n4\n6\n8\n10",
        ["p5"] = "3",
    };

    // Cached metadata references — built once from TRUSTED_PLATFORM_ASSEMBLIES
    private static readonly Lazy<IReadOnlyList<MetadataReference>> PlatformRefs = new(() =>
    {
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "";
        return trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists)
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

        // Stage 1 — static analysis (syntax errors, array-specific bugs)
        var diagnostic = _diagnosticEngine.Diagnose(request.SourceCode, skillType);

        string? actualOutput = null;

        // Stage 2 — execute and compare output (only when no compiler errors)
        if (!diagnostic.CompilerDiagnostics.Any())
        {
            // Unknown problem ID → always fail so students can't game missing entries
            if (!ExpectedOutputs.TryGetValue(request.ProblemId ?? "", out var expected))
            {
                diagnostic.IsCorrect = false;
                diagnostic.Category  = DiagnosticCategory.GenericLogicError;
                diagnostic.Message   = "Unknown problem identifier.";
            }
            else
            {
                actualOutput = await ExecuteCodeAsync(request.SourceCode);

                if (actualOutput == null)
                {
                    diagnostic.IsCorrect = false;
                    diagnostic.Category  = DiagnosticCategory.GenericLogicError;
                    diagnostic.Message   = "Your code could not be executed. Check for infinite loops or unsupported operations.";
                }
                else
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

    /// Trim each non-blank line, join with \n — makes comparison whitespace-tolerant.
    private static string Normalize(string s) =>
        string.Join('\n',
            s.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
             .Select(l => l.Trim())
             .Where(l => l.Length > 0));

    /// Wrap student code in a minimal runnable scaffold.
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

    /// Compile and execute student code; return stdout or null on failure/timeout.
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
