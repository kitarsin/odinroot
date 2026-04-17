namespace ODIN.Api.Models.Domain;

public class InteractionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid SubmissionId { get; set; }
    public string BehaviorState { get; set; } = "";
    public double HelplessnessScoreDelta { get; set; }
    public double CumulativeHelplessnessScore { get; set; }
    public double MasteryProbability { get; set; }
    public string InterventionTriggered { get; set; } = "None";
    public string DiagnosticCategory { get; set; } = "None";
    public string SkillType { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
