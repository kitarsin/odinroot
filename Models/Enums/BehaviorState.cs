namespace ODIN.Api.Models.Enums;

/// <summary>
/// The behavioral states classified by the Heuristic Behavior Detection Algorithm (HBDA).
/// Thresholds defined by the project psychologist.
/// </summary>
public enum BehaviorState
{
    /// <summary>
    /// Exploiting feedback loops to pass without engaging.
    /// Detected when: SI &lt; 2s OR paste-then-submit OR task elapsed &lt; 15s
    /// </summary>
    GamingTheSystem,

    /// <summary>
    /// Student stopped engaging after hitting an error (learned helplessness onset).
    /// Detected when: SI &gt;= 120s AND previous submission was also an error
    /// </summary>
    PostFailureDisengagement,

    /// <summary>
    /// Repeatedly submitting identical code with the same error — no structural change.
    /// Detected when: &gt;= 3 consecutive identical compiler errors AND no structural change
    /// </summary>
    WheelSpinning,

    /// <summary>
    /// Fast symbol-swapping without understanding the underlying logic.
    /// Detected when: SI &lt; 6s AND only numeric/operator swaps, no structural change
    /// </summary>
    LowProgressTrialAndError,

    /// <summary>
    /// Student is making real progress; withhold hints to let them discover the solution.
    /// Detected when: SI &gt; 15s AND a new/different error from the previous submission
    /// </summary>
    HintWithheld,

    /// <summary>
    /// Deliberate, well-paced problem solving. Optimal learning state.
    /// Detected when: SI &gt; 15s AND (correct or progressive) AND &gt;= 2 consecutive progressive submits
    /// </summary>
    ActiveThinking
}
