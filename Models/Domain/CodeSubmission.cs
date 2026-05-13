namespace ODIN.Api.Models.Domain;

/// <summary>
/// Maps to the existing `submissions` table in Supabase.
///
/// Original: id, user_id, question_id, code_snippet, is_correct, error_message, execution_time_ms, created_at
/// Added:    session_id, skill_type, keystroke fields, HBDA fields, AST fields, intervention fields
/// </summary>
public class CodeSubmission
{
    // ── Original `submissions` columns ──
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }                     // was: user_id
    public string QuestionId { get; set; } = "";          // was: question_id (puzzle identifier)
    public string SourceCode { get; set; } = "";          // maps to: code_snippet
    public bool IsCorrect { get; set; } = false;          // was: is_correct
    public string? ErrorMessage { get; set; }             // was: error_message
    public int? ExecutionTimeMs { get; set; }             // was: execution_time_ms
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow; // maps to: created_at

    // ── ODIN-specific columns (added by migration) ──
    public Guid? SessionId { get; set; }
    public string? SkillType { get; set; }

    // Keystroke dynamics
    public double AverageFlightTimeMs { get; set; }
    public double AverageDwellTimeMs { get; set; }
    public double InitialLatencyMs { get; set; }
    public double TotalTimeSeconds { get; set; }
    public double TypingBurstCoverage { get; set; }
    public int SystemCheckCount { get; set; }
    public int SelfCorrectionCount { get; set; }

    // HBDA metrics
    public int EditDistance { get; set; }
    public double SubmissionIntervalSeconds { get; set; }
    public int HintUsageCount { get; set; }
    public bool PasteDetected { get; set; }
    public double TaskElapsedSeconds { get; set; }

    /// <summary>Populated from client for HBDA only; not persisted to PostgreSQL.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int KeyDownCount { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public float? TaskBypassedDuration { get; set; }

    // AST diagnosis
    public string DiagnosticCategory { get; set; } = "None";
    public string? DiagnosticMessage { get; set; }

    // Behavioral classification
    public string? BehaviorState { get; set; }

    // Intervention
    public string InterventionType { get; set; } = "None";
}
