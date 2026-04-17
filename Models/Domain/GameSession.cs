namespace ODIN.Api.Models.Domain;

public class GameSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public int DungeonLevel { get; set; }
    public string PuzzleId { get; set; } = "";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int SubmissionCount { get; set; } = 0;
    public bool IsCompleted { get; set; } = false;

    public Player Player { get; set; } = null!;
    public ICollection<CodeSubmission> Submissions { get; set; } = new List<CodeSubmission>();
}
