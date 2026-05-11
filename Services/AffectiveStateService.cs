using ODIN.Api.Models.Enums;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Services;

/// <summary>
/// Affective State Evaluation Engine — combines HBDA and BKT outputs
/// to determine if the Helplessness Decision Gate has been crossed.
///
/// The gate triggers when:
///   1. Helplessness Score exceeds the threshold, AND
///   2. BKT mastery probability is below the uncertainty limit
///
/// Thresholds to be validated with expert psychologists.
/// </summary>
public class AffectiveStateService : IAffectiveStateService
{
    // ═══════════════════════════════════════════════════════════
    // Helplessness Decision Gate Thresholds
    // ═══════════════════════════════════════════════════════════
    private const double HelplessnessScoreThreshold = 50.0;  // Score that triggers intervention
    private const double BktUncertaintyLimit = 0.40;          // Below this P(L) = uncertain mastery
    private const double HelplessnessScoreFloor = 0.0;        // Minimum score (cannot go negative)
    private const double HelplessnessScoreCeiling = 100.0;    // Maximum score

    public AffectiveResult Evaluate(
        HbdaResult hbdaResult,
        BktResult bktResult,
        double currentHelplessnessScore)
    {
        // Apply the HBDA delta to the cumulative score
        double updatedScore = currentHelplessnessScore + hbdaResult.HelplessnessScoreDelta;

        // Clamp to valid range
        updatedScore = Math.Clamp(updatedScore, HelplessnessScoreFloor, HelplessnessScoreCeiling);

        // ── Helplessness Decision Gate ──
        // Triggers when score is high AND mastery is low
        bool helplessnessTriggered =
            updatedScore >= HelplessnessScoreThreshold &&
            bktResult.ProbabilityMastery < BktUncertaintyLimit;

        // ── Productive State Detection ──
        bool isProductiveState =
            hbdaResult.State == BehaviorState.HintWithheld ||
            hbdaResult.State == BehaviorState.ActiveThinking;

        return new AffectiveResult
        {
            HelplessnessTriggered = helplessnessTriggered,
            UpdatedHelplessnessScore = updatedScore,
            IsProductiveState = isProductiveState
        };
    }
}
