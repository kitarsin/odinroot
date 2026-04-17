namespace ODIN.Api.Models.Enums;

/// <summary>
/// The tiered intervention actions output by the Adaptive Intervention Controller.
/// Reference: Algorithm Flow — System Intervention stage.
/// </summary>
public enum InterventionType
{
    /// <summary>No intervention needed — submission is valid and student is progressing.</summary>
    None,

    /// <summary>Submission rejected — Gaming the System detected (anti-gaming protocol).</summary>
    Rejection,

    /// <summary>Scaffolded hint delivered via NPC dialogue — logic errors found or wheel-spinning detected.</summary>
    ScaffoldingHint,

    /// <summary>Positive reinforcement — Productive Failure or Active Thinking identified.</summary>
    Reward,

    /// <summary>BKT mastery threshold exceeded — next dungeon level unlocked.</summary>
    LevelUnlock
}
