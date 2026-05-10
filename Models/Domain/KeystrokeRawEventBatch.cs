namespace ODIN.Api.Models.Domain;

public class KeystrokeRawEventBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public Guid UserId { get; set; }
    public Guid? SessionId { get; set; }
    public string Events { get; set; } = "[]"; // JSONB: [[timestamp_ms, keycode, 0|1], ...]
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
