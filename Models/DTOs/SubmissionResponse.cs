namespace ODIN.Api.Models.DTOs;

/// <summary>
/// The Response Builder's formatted output delivered to the Presentation Tier's
/// Intervention &amp; Scaffolding UI. Contains everything the game client needs
/// to render the correct feedback.
/// </summary>
public class SubmissionResponse
{
    /// <summary>Unique ID for this submission record.</summary>
    public Guid SubmissionId { get; set; }

    // ── Code Diagnosis Results ──

    /// <summary>Whether the submitted code is correct.</summary>
    public bool IsCorrect { get; set; }

    /// <summary>The specific diagnostic category detected.</summary>
    public string DiagnosticCategory { get; set; } = string.Empty;

    /// <summary>Human-readable diagnostic feedback (replaces cryptic compiler errors).</summary>
    public string DiagnosticMessage { get; set; } = string.Empty;

    /// <summary>Roslyn compiler errors/warnings if any (for the code editor gutter).</summary>
    public List<CompilerDiagnosticDto> CompilerDiagnostics { get; set; } = new();

    // ── Behavioral Analysis Results ──

    /// <summary>The behavioral state classified by HBDA.</summary>
    public string BehaviorState { get; set; } = string.Empty;

    /// <summary>Current cumulative helplessness score.</summary>
    public double HelplessnessScore { get; set; }

    /// <summary>Delta applied to helplessness score this submission.</summary>
    public double HelplessnessScoreDelta { get; set; }

    // ── BKT Mastery State ──

    /// <summary>Current P(L) — probability of mastery for the tested skill.</summary>
    public double MasteryProbability { get; set; }

    /// <summary>Whether the student has achieved mastery (P(L) >= 0.90).</summary>
    public bool IsMastered { get; set; }

    /// <summary>Whether BKT is still in warm-up phase (first 3 attempts).</summary>
    public bool IsWarmUpPhase { get; set; }

    // ── Intervention Output ──

    /// <summary>The pedagogical action triggered: None, Rejection, ScaffoldingHint, Reward, LevelUnlock.</summary>
    public string InterventionType { get; set; } = string.Empty;

    /// <summary>NPC dialogue text for scaffolding hints (null if no hint).</summary>
    public NpcDialogueDto? NpcDialogue { get; set; }

    /// <summary>Whether the next dungeon level was unlocked by this submission.</summary>
    public bool LevelUnlocked { get; set; }

    /// <summary>XP reward earned (0 if rejected or incorrect).</summary>
    public int XpAwarded { get; set; }

    /// <summary>Names of any achievements unlocked by this submission (empty if none).</summary>
    public List<string> NewAchievements { get; set; } = new();
}

/// <summary>
/// Roslyn compiler diagnostic for the code editor's error gutter.
/// </summary>
public class CompilerDiagnosticDto
{
    public string Id { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
}

/// <summary>
/// NPC dialogue payload for the Intervention &amp; Scaffolding UI.
/// Transforms generic error messages into immersive, story-driven dialogue.
/// </summary>
public class NpcDialogueDto
{
    public string NpcName { get; set; } = "Odin";
    public string DialogueText { get; set; } = string.Empty;
    public string? TechnicalHint { get; set; }
    public int HintTier { get; set; }
}
