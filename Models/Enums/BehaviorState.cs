namespace ODIN.Api.Models.Enums;

/// <summary>
/// The five behavioral states classified by the Heuristic Behavior Detection Algorithm (HBDA).
/// Reference: Table 8 — Criteria of Behaviors in Stealth Assessment
/// </summary>
public enum BehaviorState
{
    /// <summary>
    /// Mindless modification without understanding.
    /// Detected when: SI &lt;= 10s, ED &lt;= 2 chars, Result = Error
    /// </summary>
    Tinkering,

    /// <summary>
    /// Exploiting feedback mechanisms to pass without learning.
    /// Detected when: SI &lt;= 5s, HU &gt;= 3, ED ~= 0
    /// </summary>
    GamingTheSystem,

    /// <summary>
    /// Learned Helplessness / Lack of Mastery. The primary intervention target.
    /// Detected when: TT &gt;= 120s, Attempts &gt;= 10, EC = Same Logic Error
    /// </summary>
    WheelSpinning,

    /// <summary>
    /// Struggling but actively learning/refining. Healthy behavior.
    /// Detected when: Attempts &gt;= 3, EV = error changes, ED &gt;= 10 chars
    /// </summary>
    ProductiveFailure,

    /// <summary>
    /// Cognitive planning followed by execution. Optimal state.
    /// Detected when: IL &gt;= 30s, KF &lt;= 200ms, Result = Success/Near-Success
    /// </summary>
    ActiveThinking
}
