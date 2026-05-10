namespace ODIN.Api.Services.Interfaces;

public interface ICodeExecutionService
{
    Task<string?> ExecuteAsync(string sourceCode);
    string Normalize(string output);
}
