using ODIN.Api.Models.Domain;
using ODIN.Api.Models.Enums;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Services;

/// <summary>
/// Heuristic Behavior Detection Algorithm (HBDA) — Stage 1 of the Sequential Pipeline.
/// Classifies student submissions into five behavioral states using thresholds
/// from Table 8: Criteria of Behaviors in Stealth Assessment.
///
/// Helplessness Score weights function as a manual GAM:
///   +20 for Gaming the System (rapid guessing)
///   +15 for Wheel-Spinning (stuck on same error)
///   +10 for Tinkering (mindless modification)
///   -10 for Active Thinking (cognitive planning)
///   -15 for Productive Failure (strategic variation)
/// </summary>
public class HbdaService : IHbdaService
{
    // ═══════════════════════════════════════════════════════════
    // Configurable Thresholds (to be calibrated with psychologists)
    // ═══════════════════════════════════════════════════════════

    // Gaming the System
    private const double GamingSubmissionIntervalMax = 5.0;    // SI <= 5s
    private const int    GamingHintUsageMin = 3;               // HU >= 3
    private const int    GamingEditDistanceMax = 0;            // ED ~= 0

    // Tinkering
    private const double TinkeringSubmissionIntervalMax = 10.0; // SI <= 10s
    private const int    TinkeringEditDistanceMax = 2;          // ED <= 2 chars

    // Wheel-Spinning
    private const double WheelSpinningTotalTimeMin = 120.0;     // TT >= 120s
    private const int    WheelSpinningAttemptsMin = 10;         // Attempts >= 10
    // EC = Same Logic Error (checked via error consistency)

    // Productive Failure
    private const int    ProductiveFailureAttemptsMin = 3;      // Attempts >= 3
    private const int    ProductiveFailureEditDistanceMin = 10; // ED >= 10 chars
    // EV = Error type changes (syntax -> logic)

    // Active Thinking
    private const double ActiveThinkingInitialLatencyMin = 30_000; // IL >= 30s (in ms)
    private const double ActiveThinkingFlightTimeMax = 200;        // KF <= 200ms

    // Helplessness Score Weights (GAM-style)
    private const double WeightGaming = 20.0;
    private const double WeightWheelSpinning = 15.0;
    private const double WeightTinkering = 10.0;
    private const double WeightActiveThinking = -10.0;
    private const double WeightProductiveFailure = -15.0;

    public HbdaResult Classify(
        CodeSubmission current,
        CodeSubmission? previous,
        List<CodeSubmission> sessionHistory)
    {
        // Priority order: Gaming > Tinkering > Wheel-Spinning > Productive Failure > Active Thinking
        // Gaming is checked first as it should be immediately rejected.

        if (IsGamingTheSystem(current))
        {
            return new HbdaResult
            {
                State = BehaviorState.GamingTheSystem,
                HelplessnessScoreDelta = WeightGaming,
                Reasoning = $"Gaming detected: SI={current.SubmissionIntervalSeconds:F1}s, " +
                           $"HU={current.HintUsageCount}, ED={current.EditDistance}"
            };
        }

        if (IsTinkering(current))
        {
            return new HbdaResult
            {
                State = BehaviorState.Tinkering,
                HelplessnessScoreDelta = WeightTinkering,
                Reasoning = $"Tinkering detected: SI={current.SubmissionIntervalSeconds:F1}s, " +
                           $"ED={current.EditDistance} chars"
            };
        }

        if (IsWheelSpinning(current, sessionHistory))
        {
            return new HbdaResult
            {
                State = BehaviorState.WheelSpinning,
                HelplessnessScoreDelta = WeightWheelSpinning,
                Reasoning = $"Wheel-Spinning detected: TT={current.TotalTimeSeconds:F0}s, " +
                           $"Attempts={sessionHistory.Count}, same error repeated"
            };
        }

        if (IsProductiveFailure(current, previous, sessionHistory))
        {
            return new HbdaResult
            {
                State = BehaviorState.ProductiveFailure,
                HelplessnessScoreDelta = WeightProductiveFailure,
                Reasoning = $"Productive Failure: ED={current.EditDistance} chars, " +
                           $"error type changed from previous submission"
            };
        }

        if (IsActiveThinking(current))
        {
            return new HbdaResult
            {
                State = BehaviorState.ActiveThinking,
                HelplessnessScoreDelta = WeightActiveThinking,
                Reasoning = $"Active Thinking: IL={current.InitialLatencyMs:F0}ms pause, " +
                           $"KF={current.AverageFlightTimeMs:F0}ms burst typing"
            };
        }

        // Default: classify as Tinkering with reduced weight if no clear pattern
        return new HbdaResult
        {
            State = BehaviorState.Tinkering,
            HelplessnessScoreDelta = WeightTinkering * 0.5,
            Reasoning = "No clear behavioral pattern — defaulting to mild tinkering classification"
        };
    }

    // ═══════════════════════════════════════════════════════════
    // Individual State Detectors
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Gaming: SI &lt;= 5s, HU &gt;= 3, ED ~= 0 (spamming compile, seeking answers)
    /// </summary>
    private static bool IsGamingTheSystem(CodeSubmission current)
    {
        bool rapidSubmission = current.SubmissionIntervalSeconds <= GamingSubmissionIntervalMax;
        bool excessiveHints = current.HintUsageCount >= GamingHintUsageMin;
        bool noAttemptToSolve = current.EditDistance <= GamingEditDistanceMax;

        // Either rapid + no changes, or excessive hint seeking
        return (rapidSubmission && noAttemptToSolve) || (rapidSubmission && excessiveHints);
    }

    /// <summary>
    /// Tinkering: SI &lt;= 10s, ED &lt;= 2 chars, Result = Error (mindless modification)
    /// </summary>
    private static bool IsTinkering(CodeSubmission current)
    {
        return current.SubmissionIntervalSeconds <= TinkeringSubmissionIntervalMax
            && current.EditDistance <= TinkeringEditDistanceMax
            && current.EditDistance > 0  // Distinguishes from Gaming (ED ~= 0)
            && !current.IsCorrect;
    }

    /// <summary>
    /// Wheel-Spinning: TT &gt;= 120s, Attempts &gt;= 10, same logic error repeated (no progress)
    /// </summary>
    private static bool IsWheelSpinning(CodeSubmission current, List<CodeSubmission> history)
    {
        if (history.Count < WheelSpinningAttemptsMin) return false;

        // Check total accumulated time across session
        double totalSessionTime = history.Sum(h => h.TotalTimeSeconds);
        if (totalSessionTime < WheelSpinningTotalTimeMin) return false;

        // Check error consistency: same diagnostic category in the last 3+ submissions
        var recentErrors = history
            .OrderByDescending(h => h.SubmittedAt)
            .Take(3)
            .Select(h => h.DiagnosticCategory)
            .Distinct()
            .ToList();

        // If the last 3 submissions all have the same error type = stuck
        return recentErrors.Count == 1 && recentErrors[0] != "None";
    }

    /// <summary>
    /// Productive Failure: Attempts &gt;= 3, error type changes (EV), ED &gt;= 10 chars (structural rewrite)
    /// </summary>
    private static bool IsProductiveFailure(
        CodeSubmission current,
        CodeSubmission? previous,
        List<CodeSubmission> history)
    {
        if (history.Count < ProductiveFailureAttemptsMin) return false;
        if (current.EditDistance < ProductiveFailureEditDistanceMin) return false;

        // Error variety: the error type must have changed from the previous submission
        if (previous == null) return false;
        bool errorChanged = current.DiagnosticCategory != previous.DiagnosticCategory;
        bool notCorrect = !current.IsCorrect;

        return errorChanged && notCorrect;
    }

    /// <summary>
    /// Active Thinking: IL &gt;= 30s (pause for thought), KF &lt;= 200ms (burst typing),
    /// Result = Success or Near-Success
    /// </summary>
    private static bool IsActiveThinking(CodeSubmission current)
    {
        bool longPause = current.InitialLatencyMs >= ActiveThinkingInitialLatencyMin;
        bool burstTyping = current.AverageFlightTimeMs <= ActiveThinkingFlightTimeMax;

        return longPause && burstTyping;
    }
}
