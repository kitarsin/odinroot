namespace ODIN.Api.Models.Domain;

public class Puzzle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int DungeonLevel { get; set; }
    public int OrderIndex { get; set; }
    public string SkillType { get; set; } = "";
    public string StarterCode { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public string? ArrayConcept { get; set; }
    public bool IsActive { get; set; } = true;
    // JSONB: array of SecondaryTestCase for anti-hardcoding validation
    public string? TestCases { get; set; }
}
