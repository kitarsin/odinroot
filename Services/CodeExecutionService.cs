using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Services;

/// <summary>
/// Compiles and executes student C# code fragments in-process using Roslyn.
/// Shared by SubmissionController (live puzzles) and PretestController (diagnostic).
/// </summary>
public class CodeExecutionService : ICodeExecutionService
{
    // Cached metadata references built once from TRUSTED_PLATFORM_ASSEMBLIES.
    private static readonly Lazy<IReadOnlyList<MetadataReference>> PlatformRefs = new(() =>
    {
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "";
        return trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
    });

    // Prevents concurrent Console.SetOut calls across parallel requests.
    private static readonly object ConsoleLock = new();

    public async Task<string?> ExecuteAsync(string sourceCode)
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

    public string Normalize(string output) =>
        string.Join('\n',
            output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
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

    private static string? RunCode(string sourceCode)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(WrapForExecution(sourceCode));
            var compilation = CSharpCompilation.Create(
                assemblyName: $"Sub_{Guid.NewGuid():N}",
                syntaxTrees: [tree],
                references: PlatformRefs.Value,
                options: new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            using var ms = new MemoryStream();
            if (!compilation.Emit(ms).Success) return null;

            ms.Seek(0, SeekOrigin.Begin);
            var asm   = Assembly.Load(ms.ToArray());
            var entry = asm.EntryPoint ?? throw new InvalidOperationException("No entry point.");
            var argv  = entry.GetParameters().Length == 0 ? null : new object[] { Array.Empty<string>() };

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

    // Caps output at 4 KB — any correct puzzle solution outputs at most a few lines.
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
