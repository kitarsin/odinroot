using System.ComponentModel.DataAnnotations;

namespace ODIN.Api.Models.DTOs;

/// <summary>
/// The structured JSON payload sent by the Game Client's Keystroke Dynamics Monitor
/// and Integrated Code Editor. This is the single endpoint contract the game calls.
/// </summary>
public class SubmissionRequest
{
    [Required]
    public Guid PlayerId { get; set; }

    [Required]
    public Guid SessionId { get; set; }

    [Required]
    public string PuzzleId { get; set; } = string.Empty;

    [Required]
    public string SkillType { get; set; } = string.Empty;

    [Required]
    public string SourceCode { get; set; } = string.Empty;

    [Required]
    public KeystrokePayload KeystrokeData { get; set; } = new();

    public int HintUsageCount { get; set; } = 0;
}

/// <summary>
/// Keystroke dynamics captured at millisecond precision by the client.
/// </summary>
public class KeystrokePayload
{
    public double AverageFlightTimeMs { get; set; }
    public double AverageDwellTimeMs { get; set; }
    public double InitialLatencyMs { get; set; }
    public double TotalTimeSeconds { get; set; }
    public List<double[]>? RawEvents { get; set; }
}
