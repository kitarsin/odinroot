using ODIN.Api.Models.DTOs;
using ODIN.Api.Models.Enums;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Services;

public class InterventionControllerService : IInterventionController
{
    private const int XpCorrectAnswer  = 100;
    private const int XpActiveThinking = 50;
    private const int XpMasteryBonus   = 500;

    // Psychologist-authored dialogue for each behavioral state.
    private static NpcDialogueDto Dialogue(string text) =>
        new() { NpcName = "Odin", DialogueText = text };

    public Task<InterventionResult> DetermineInterventionAsync(
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
            return Task.FromResult(result);
        }

        // Correct (not yet mastered)
        if (diagnosticResult.IsCorrect)
        {
            result.Type = InterventionType.None;
            result.XpAwarded = XpCorrectAnswer;
            return Task.FromResult(result);
        }

        // All remaining cases are incorrect submissions — apply behavioral intervention.
        switch (behaviorState)
        {
            case BehaviorState.GamingTheSystem:
                result.Type = InterventionType.Rejection;
                result.NpcDialogue = Dialogue(
                    "I noticed you skipped ahead quickly. Before submitting, try walking through " +
                    "your code line by line—does each part do what you think it does?");
                break;

            case BehaviorState.PostFailureDisengagement:
                result.Type = InterventionType.ScaffoldingHint;
                result.NpcDialogue = Dialogue(
                    "Looks like that last error stopped you cold. It happens to the best of us. " +
                    "Take a breath and look at the line where it broke—what does the message actually say?");
                break;

            case BehaviorState.WheelSpinning:
                result.Type = InterventionType.ScaffoldingHint;
                result.NpcDialogue = Dialogue(
                    "You've submitted the same code a few times—I think something in the structure " +
                    "itself needs to change. Look at the overall shape of your solution, not just the flagged line.");
                break;

            case BehaviorState.LowProgressTrialAndError:
                result.Type = InterventionType.ScaffoldingHint;
                result.NpcDialogue = Dialogue(
                    "Slow down—you're swapping symbols fast, but the logic hasn't shifted. " +
                    "What is the line supposed to do? Try explaining it out loud.");
                break;

            case BehaviorState.HintWithheld:
                // Student is making real progress; encourage without giving a hint.
                result.Type = InterventionType.Reward;
                result.NpcDialogue = Dialogue(
                    "You've been stuck on this for a bit. You're making progress—each error is a clue. " +
                    "What changed between your last two tries?");
                break;

            case BehaviorState.ActiveThinking:
                result.Type = InterventionType.Reward;
                result.XpAwarded = XpActiveThinking;
                break;

            default:
                result.Type = InterventionType.None;
                break;
        }

        return Task.FromResult(result);
    }
}
