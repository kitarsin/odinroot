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
    /// "Retained" states per the psychologist's §5.2 transition model.
    /// The system keeps the previous label when the same state recurs —
    /// no re-intervention fires unless the state has genuinely changed.
    /// GamingTheSystem is intentionally excluded: every paste is a fresh
    /// deliberate action that must always be intercepted.
    /// </summary>
    private static readonly HashSet<string> RetainedStates = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(BehaviorState.ActiveThinking),
        nameof(BehaviorState.WheelSpinning),
        "Normal",
        "",
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
        // First submission establishes the baseline - never trigger dialogue.
        if (isFirstSubmission)
        {
            if (bktResult.IsMastered && diagnosticResult.IsCorrect)
                result.XpAwarded = XpCorrectAnswer + XpMasteryBonus;
            else if (diagnosticResult.IsCorrect)
                result.XpAwarded = XpCorrectAnswer;
            result.Type = InterventionType.None;
            return result;
        }

        // ── GamingTheSystem — Highest Priority ──
        // Must be checked BEFORE correctness rewards.
        // A pasted correct answer is NOT a genuine solution; the student must
        // engage with the problem deliberately. Each paste is an independent
        // action so we do NOT apply the retention gate here.
        if (behaviorState == BehaviorState.GamingTheSystem)
        {
            result.Type       = InterventionType.Rejection;
            result.NpcDialogue = await FetchScaffoldingHintAsync(
                BehaviorState.GamingTheSystem.ToString(),
                diagnosticResult.Category.ToString(),
                skillType.ToString(),
                currentHintTier);
            // XpAwarded intentionally left at 0 - rejected submission earns nothing
            return result;
        }

        // ── Mastery + Correct -> Level Unlock ──
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
        // Retained labels suppress re-intervention when the same state persists.
        // An intervention only fires on a meaningful state change.
        //
        // Examples:
        //   WS -> WS  (retained): silent - still wheel-spinning, no new signal
        //   AT -> PD  (transition): fire PD hint - moved from productive to stuck
        //   PD -> TE  (transition): fire TE hint - now rapid-cycling
        string currentStateName = behaviorState.ToString();
        bool isSameState = currentStateName.Equals(
            previousBehaviorState, StringComparison.OrdinalIgnoreCase);

        if (isSameState && RetainedStates.Contains(currentStateName))
        {
            // State retained - stay silent, no new intervention
            result.Type = InterventionType.None;
            return result;
        }

        // ── Observable Behavioural Interventions ──
        // All dialogue is fetched from the DB (no hardcoded strings).
        result.Type       = InterventionType.ScaffoldingHint;
        result.NpcDialogue = await FetchScaffoldingHintAsync(
            behaviorState.ToString(),
            diagnosticResult.Category.ToString(),
            skillType.ToString(),
            currentHintTier);

        return result;
    }

    /// <summary>
    /// Fetch the most appropriate scaffolding hint from the DB.
    ///
    /// Lookup cascade (first match wins):
    ///   1. Behavioral state + exact skill + exact tier   (most specific)
    ///   2. Behavioral state + any skill + any tier <=requested
    ///   3. Diagnostic category + any skill + any tier <=requested (error fallback)
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
