using Microsoft.EntityFrameworkCore;
using ODIN.Api.Data;
using ODIN.Api.Models.DTOs;
using ODIN.Api.Models.Enums;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Services;

public class InterventionControllerService : IInterventionController
{
    private readonly OdinDbContext _db;

    private const int XpCorrectAnswer = 100;
    private const int XpProductiveFailure = 25;
    private const int XpActiveThinking = 50;
    private const int XpMasteryBonus = 500;

    public InterventionControllerService(OdinDbContext db) { _db = db; }

    public async Task<InterventionResult> DetermineInterventionAsync(
        BehaviorState behaviorState, DiagnosticResult diagnosticResult,
        BktResult bktResult, AffectiveResult affectiveResult,
        SkillType skillType, int currentHintTier)
    {
        var result = new InterventionResult();

        if (behaviorState == BehaviorState.GamingTheSystem)
        {
            result.Type = InterventionType.Rejection;
            result.NpcDialogue = await FetchHintAsync("None", "ArrayInitialization", 0, "Rushing");
            return result;
        }

        if (bktResult.IsMastered && diagnosticResult.IsCorrect)
        {
            result.Type = InterventionType.LevelUnlock;
            result.LevelUnlocked = true;
            result.XpAwarded = XpCorrectAnswer + XpMasteryBonus;
            return result;
        }

        if (diagnosticResult.IsCorrect)
        {
            result.Type = InterventionType.None;
            result.XpAwarded = XpCorrectAnswer;
            return result;
        }

        if (behaviorState == BehaviorState.ProductiveFailure)
        {
            result.Type = InterventionType.Reward;
            result.NpcDialogue = await FetchHintAsync("None", "ArrayInitialization", 0, "persistence");
            result.XpAwarded = XpProductiveFailure;
            return result;
        }

        if (behaviorState == BehaviorState.ActiveThinking)
        {
            result.Type = InterventionType.Reward;
            result.XpAwarded = XpActiveThinking;
            return result;
        }

        if (affectiveResult.HelplessnessTriggered ||
            behaviorState == BehaviorState.WheelSpinning ||
            behaviorState == BehaviorState.Tinkering)
        {
            int hintTier = Math.Min(currentHintTier + 1, 3);
            string diagCat = diagnosticResult.Category.ToString();
            string skill = skillType.ToString();

            var hint = await _db.ScaffoldingHints
                .Where(h => h.IsActive && h.DiagnosticCategory == diagCat && h.SkillType == skill && h.Tier == hintTier)
                .FirstOrDefaultAsync()
                ?? await _db.ScaffoldingHints
                    .Where(h => h.IsActive && h.DiagnosticCategory == diagCat && h.Tier <= hintTier)
                    .OrderByDescending(h => h.Tier).FirstOrDefaultAsync()
                ?? await _db.ScaffoldingHints
                    .Where(h => h.IsActive && h.DiagnosticCategory == "GenericLogicError")
                    .FirstOrDefaultAsync();

            result.Type = InterventionType.ScaffoldingHint;
            if (hint != null)
                result.NpcDialogue = new NpcDialogueDto
                {
                    NpcName = hint.NpcName, DialogueText = hint.DialogueText,
                    TechnicalHint = hint.TechnicalHint, HintTier = hint.Tier
                };
            return result;
        }

        return result;
    }

    private async Task<NpcDialogueDto?> FetchHintAsync(string diagCat, string skill, int tier, string contains)
    {
        var hint = await _db.ScaffoldingHints
            .Where(h => h.IsActive && h.Tier == tier && h.DialogueText.Contains(contains))
            .FirstOrDefaultAsync();
        if (hint == null) return new NpcDialogueDto { NpcName = "Odin", DialogueText = "Take a moment to think, warrior.", HintTier = 0 };
        return new NpcDialogueDto { NpcName = hint.NpcName, DialogueText = hint.DialogueText, TechnicalHint = hint.TechnicalHint, HintTier = hint.Tier };
    }
}
