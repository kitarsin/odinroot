namespace ODIN.Api.Models.Domain;

public class ScaffoldingHint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DiagnosticCategory { get; set; } = "";
    public string SkillType { get; set; } = "";
    public int Tier { get; set; } = 1;
    public string NpcName { get; set; } = "Odin";
    public string DialogueText { get; set; } = "";
    public string? TechnicalHint { get; set; }
    public bool IsActive { get; set; } = true;
}
