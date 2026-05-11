using Microsoft.EntityFrameworkCore;
using ODIN.Api.Data;
using ODIN.Api.Models.DTOs;
using ODIN.Api.Models.Enums;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Services;

public class InterventionControllerService(OdinDbContext db) : IInterventionController
{
    private const int XpCorrectAnswer  = 100;
    private const int XpActiveThinking = 50;
    private const int XpMasteryBonus   = 500;

    /// <summary>
    /// States marked "retained" by the psychologist's transition model.
    /// If the student is already in one of these states, do NOT re-intervene —
    /// only a genuine state *transition* warrants a new intervention.
    /// </summary>
    private static readonly HashSet<string> RetainedStates = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(BehaviorState.ActiveThinking),
        nameof(BehaviorState.WheelSpinning),
        nameof(BehaviorState.GamingTheSystem),
        "Normal",   // default HBDA fallback
        "",         // no prior state (first submission)
    };

    public async Task<InterventionResult> DetermineInterventionAsync(
        BehaviorState behaviorState,
        DiagnosticResult diagnosticResult,
        BktResult bktResult,
        SkillType skillType,
        int currentHintTier,
        bool isFirstSubmission,
        string previousBehaviorState)
    {
        var result = new InterventionResult();

        // ── Phase 1 Baseline Guard ──
        // First submission establishes the baseline — never trigger dialogue.
        if (isFirstSubmission)
        {
            if (bktResult.IsMastered && diagnosticResult.IsCorrect)
                result.XpAwarded = XpCorrectAnswer + XpMasteryBonus;
            else if (diagnosticResult.IsCorrect)
                result.XpAwarded = XpCorrectAnswer;
            result.Type = InterventionType.None;
            return result;
        }

        // ── Mastery + Correct → Level Unlock ──
        if (bktResult.IsMastered && diagnosticResult.IsCorrect)
        {
            result.Type        = InterventionType.LevelUnlock;
            result.LevelUnlocked = true;
            result.XpAwarded   = XpCorrectAnswer + XpMasteryBonus;
            return result;
        }

        // ── Correct (not yet mastered) ──
        if (diagnosticResult.IsCorrect)
        {
            result.Type      = InterventionType.None;
            result.XpAwarded = XpCorrectAnswer;
            return result;
        }

        // ── Silent Productive States ──
        // HintWithheld and ActiveThinking represent genuine cognitive effort.
        // ODIN must remain completely silent to preserve productive struggle.
        if (behaviorState == BehaviorState.HintWithheld)
        {
            result.Type = InterventionType.None;
            return result;
        }

        if (behaviorState == BehaviorState.ActiveThinking)
        {
            result.Type      = InterventionType.None;
            result.XpAwarded = XpActiveThinking;
            return result;
        }

        // ── State-Transition Gate (psychologist §5.2) ──
        // "Retained" labels mean the system keeps the previous state when
        // the same behaviour is detected again — no re-intervention fires.
        // An intervention only triggers on a meaningful state *change*.
        //
        // Examples:
        //   WS → WS  (retained): silent — student is still wheel-spinning, no new signal
        //   AT → PD  (transition): fire PD hint — student moved from productive to stuck
        //   PD → TE  (transition): fire TE hint — student is now rapid-cycling
        //   GS → GS  (retained): silent — already rejected once, no need to repeat
        string currentStateName = behaviorState.ToString();
        bool isSameState = currentStateName.Equals(
            previousBehaviorState, StringComparison.OrdinalIgnoreCase);

        if (isSameState && RetainedStates.Contains(currentStateName))
        {
            // State retained — stay silent, no new intervention
            result.Type = InterventionType.None;
            return result;
        }

        // ── Observable Behavioural Interventions ──
        // All dialogue is fetched from the DB (no hardcoded strings).

        var diagCategory = diagnosticResult.Category.ToString();
        var skill        = skillType.ToString();

        if (behaviorState == BehaviorState.GamingTheSystem)
        {
            result.Type       = InterventionType.Rejection;
            result.NpcDialogue = await FetchScaffoldingHintAsync(
                BehaviorState.GamingTheSystem.ToString(), diagCategory, skill, currentHintTier);
            return result;
        }

        // PostFailureDisengagement, WheelSpinning, LowProgressTrialAndError
        result.Type       = InterventionType.ScaffoldingHint;
        result.NpcDialogue = await FetchScaffoldingHintAsync(
            behaviorState.ToString(), diagCategory, skill, currentHintTier);

        return result;
    }

    /// <summary>
    /// Fetch the most appropriate scaffolding hint from the DB.
    ///
    /// Lookup cascade (first match wins):
    ///   1. Behavioral state + exact skill + exact tier   (most specific)
    ///   2. Behavioral state + any skill + any tier ≤ requested
    ///   3. Diagnostic category + any skill + any tier ≤ requested (error-based fallback)
    ///   4. GenericFallback catch-all
    ///
    /// Behavioral hints are stored with diagnostic_category = behaviorState.ToString()
    /// so no schema changes are needed (Option A).
    /// </summary>
    private async Task<NpcDialogueDto?> FetchScaffoldingHintAsync(
        string behaviorState, string diagCategory, string skill, int currentHintTier)
    {
        int tier = Math.Min(currentHintTier + 1, 3);

        var hint =
            // 1. Exact: behavior + skill + tier
            await db.ScaffoldingHints
                .Where(h => h.IsActive
                    && h.DiagnosticCategory == behaviorState
                    && h.SkillType          == skill
                    && h.Tier               == tier)
                .FirstOrDefaultAsync()

            // 2. Behavior + any skill/tier
            ?? await db.ScaffoldingHints
                .Where(h => h.IsActive
                    && h.DiagnosticCategory == behaviorState
                    && h.Tier               <= tier)
                .OrderByDescending(h => h.Tier)
                .FirstOrDefaultAsync()

            // 3. Error-category fallback
            ?? await db.ScaffoldingHints
                .Where(h => h.IsActive
                    && h.DiagnosticCategory == diagCategory
                    && h.Tier               <= tier)
                .OrderByDescending(h => h.Tier)
                .FirstOrDefaultAsync()

            // 4. Generic catch-all
            ?? await db.ScaffoldingHints
                .Where(h => h.IsActive && h.DiagnosticCategory == "GenericFallback")
                .FirstOrDefaultAsync();

        if (hint == null) return null;

        return new NpcDialogueDto
        {
            NpcName       = hint.NpcName,
            DialogueText  = hint.DialogueText,
            TechnicalHint = hint.TechnicalHint,
            HintTier      = hint.Tier
        };
    }
}
