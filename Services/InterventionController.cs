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

    public async Task<InterventionResult> DetermineInterventionAsync(
        BehaviorState behaviorState,
        DiagnosticResult diagnosticResult,
        BktResult bktResult,
        SkillType skillType,
        int currentHintTier,
        bool isFirstSubmission)
    {
        var result = new InterventionResult();

        // ── Phase 1 Baseline Guard ──
        // The first submission in a session establishes the baseline.
        // No intervention dialogue is ever triggered, regardless of correctness.
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

        // ── Observable Behavioral Interventions ──
        // All states below trigger strictly based on observable behaviour thresholds.
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
        // → all route to the DB-backed tiered scaffolding system
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

        // 1. Exact: behavior + skill + tier
        var hint =
            await db.ScaffoldingHints
                .Where(h => h.IsActive
                    && h.DiagnosticCategory == behaviorState
                    && h.SkillType          == skill
                    && h.Tier               == tier)
                .FirstOrDefaultAsync()

            // 2. Behavior + any skill/tier (catches the "All" skill rows)
            ?? await db.ScaffoldingHints
                .Where(h => h.IsActive
                    && h.DiagnosticCategory == behaviorState
                    && h.Tier               <= tier)
                .OrderByDescending(h => h.Tier)
                .FirstOrDefaultAsync()

            // 3. Error-category hint (the original error-specific path)
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
            NpcName      = hint.NpcName,
            DialogueText = hint.DialogueText,
            TechnicalHint = hint.TechnicalHint,
            HintTier     = hint.Tier
        };
    }
}
