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

    public bool IsHintRequest { get; set; }
}

/// <summary>
/// Keystroke dynamics captured at millisecond precision by the client.
/// </summary>
public class KeystrokePayload
{
    public double AverageFlightTimeMs { get; set; }
    public double AverageDwellTimeMs  { get; set; }
    public double InitialLatencyMs    { get; set; }
    public double TotalTimeSeconds    { get; set; }
    public List<double[]>? RawEvents  { get; set; }

    /// <summary>True when the client detected a Ctrl+V paste immediately before submit.</summary>
    public bool PasteDetected { get; set; }

    // ── 4-Phase Telemetry ──

    /// <summary>Seconds the student was idle (no keystrokes) at the moment of submit.</summary>
    public double InactivityDuration { get; set; }

    /// <summary>Seconds elapsed since the student's previous submission (client-measured).</summary>
    public double TimeSinceLastSubmit { get; set; }

    /// <summary>
    /// Accumulated error history since the last intervention reset.
    /// Grows across multiple incorrect submissions; cleared when a ScaffoldingHint fires.
    /// </summary>
    public List<ErrorLogEntry> ErrorLog { get; set; } = new();

    /// <summary>
    /// True on the very first submission of a session (Phase 1 baseline).
    /// When true, the server must not trigger any intervention dialogue.
    /// </summary>
    public bool IsFirstSubmission { get; set; }
}

/// <summary>
/// A single entry in the client-side error accumulation log.
/// </summary>
public class ErrorLogEntry
{
    public string Category { get; set; } = string.Empty;
    public string Message  { get; set; } = string.Empty;
}
