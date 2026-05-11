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

    private static NpcDialogueDto Dialogue(string text) =>
        new() { NpcName = "Odin", DialogueText = text };

    public async Task<InterventionResult> DetermineInterventionAsync(
        BehaviorState behaviorState, DiagnosticResult diagnosticResult,
        BktResult bktResult, AffectiveResult affectiveResult,
        SkillType skillType, int currentHintTier)
    {
        var result = new InterventionResult();

        // Mastery + correct → unlock next level
        if (bktResult.IsMastered && diagnosticResult.IsCorrect)
        {
            result.Type = InterventionType.LevelUnlock;
            result.LevelUnlocked = true;
            result.XpAwarded = XpCorrectAnswer + XpMasteryBonus;
            return result;
        }

        // Correct (not yet mastered)
        if (diagnosticResult.IsCorrect)
        {
            result.Type = InterventionType.None;
            result.XpAwarded = XpCorrectAnswer;
            return result;
        }

        // Gaming — reject before any hint logic runs.
        if (behaviorState == BehaviorState.GamingTheSystem)
        {
            result.Type = InterventionType.Rejection;
            result.NpcDialogue = Dialogue(
                "I noticed you skipped ahead quickly. Before submitting, try walking through " +
                "your code line by line - does each part do what you think it does?");
            return result;
        }

        // Helplessness gate — score is approaching the threshold.
        // Show a DB scaffolding hint targeted to the student's specific error
        // to prevent further disengagement before it sets in.
        if (affectiveResult.HelplessnessTriggered)
        {
            result.Type = InterventionType.ScaffoldingHint;
            result.NpcDialogue = await FetchScaffoldingHintAsync(
                diagnosticResult.Category.ToString(), skillType.ToString(), currentHintTier);
            return result;
        }

        // Remaining behavioural interventions (helplessness not yet triggered).
        switch (behaviorState)
        {
            case BehaviorState.PostFailureDisengagement:
                result.Type = InterventionType.ScaffoldingHint;
                result.NpcDialogue = Dialogue(
                    "Looks like that last error stopped you cold. It happens to the best of us. " +
                    "Take a breath and look at the line where it broke - what does the message actually say?");
                break;

            case BehaviorState.WheelSpinning:
                result.Type = InterventionType.ScaffoldingHint;
                result.NpcDialogue = Dialogue(
                    "You've submitted the same code a few times - I think something in the structure " +
                    "itself needs to change. Look at the overall shape of your solution, not just the flagged line.");
                break;

            case BehaviorState.LowProgressTrialAndError:
                result.Type = InterventionType.ScaffoldingHint;
                result.NpcDialogue = Dialogue(
                    "Slow down - you're swapping symbols fast, but the logic hasn't shifted. " +
                    "What is the line supposed to do? Try explaining it out loud.");
                break;

            case BehaviorState.HintWithheld:
                result.Type = InterventionType.Reward;
                result.NpcDialogue = Dialogue(
                    "You've been stuck on this for a bit. You're making progress - each error is a clue. " +
                    "What changed between your last two tries?");
                break;

            case BehaviorState.ActiveThinking:
                result.Type = InterventionType.Reward;
                result.XpAwarded = XpActiveThinking;
                break;

            default:
                result.Type = InterventionType.None;
                result.NpcDialogue = Dialogue(
                    "Not quite — check what value your code actually prints versus what the problem asks for.");
                break;
        }

        return result;
    }

    /// Fetch the most appropriate scaffolding hint for the student's current error.
    /// Tries an exact match (category + skill + tier), then loosens to any tier,
    /// then falls back to GenericLogicError.
    private async Task<NpcDialogueDto?> FetchScaffoldingHintAsync(
        string diagCategory, string skill, int currentHintTier)
    {
        int tier = Math.Min(currentHintTier + 1, 3);

        var hint =
            await db.ScaffoldingHints
                .Where(h => h.IsActive
                    && h.DiagnosticCategory == diagCategory
                    && h.SkillType == skill
                    && h.Tier == tier)
                .FirstOrDefaultAsync()
            ?? await db.ScaffoldingHints
                .Where(h => h.IsActive
                    && h.DiagnosticCategory == diagCategory
                    && h.Tier <= tier)
                .OrderByDescending(h => h.Tier)
                .FirstOrDefaultAsync()
            ?? await db.ScaffoldingHints
                .Where(h => h.IsActive && h.DiagnosticCategory == "GenericLogicError")
                .FirstOrDefaultAsync();

        if (hint == null) return null;

        return new NpcDialogueDto
        {
            NpcName = hint.NpcName,
            DialogueText = hint.DialogueText,
            TechnicalHint = hint.TechnicalHint,
            HintTier = hint.Tier
        };
    }
}
