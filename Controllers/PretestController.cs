using Microsoft.AspNetCore.Mvc;
using ODIN.Api.Models.Enums;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Controllers;

[ApiController]
[Route("api/pretest")]
public class PretestController : ControllerBase
{
    private readonly IDiagnosticEngine _diagnosticEngine;

    public PretestController(IDiagnosticEngine diagnosticEngine)
    {
        _diagnosticEngine = diagnosticEngine;
    }

    [HttpPost("compile")]
    public IActionResult Compile([FromBody] PretestCompileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceCode))
            return BadRequest(new { error = "Source code is required" });

        if (!Enum.TryParse<SkillType>(request.SkillType, out var skillType))
            skillType = SkillType.ArrayInitialization;

        var result = _diagnosticEngine.Diagnose(request.SourceCode, skillType);

        return Ok(new
        {
            isCorrect = result.IsCorrect,
            diagnosticCategory = result.Category.ToString(),
            diagnosticMessage = result.Message,
            compilerDiagnostics = result.CompilerDiagnostics.Select(d => new
            {
                id = d.Id,
                severity = d.Severity,
                message = d.Message,
                line = d.Line,
                column = d.Column
            })
        });
    }
}

public class PretestCompileRequest
{
    public string SourceCode { get; set; } = "";
    public string SkillType { get; set; } = "ArrayInitialization";
}
