using Microsoft.EntityFrameworkCore;
using ODIN.Api.Data;
using ODIN.Api.Models.Domain;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Services;

public class BktService : IBktService
{
    private readonly OdinDbContext _db;

    private const double P_L0 = 0.10;
    private const double P_T  = 0.10;
    private const double P_G  = 0.20;
    private const double P_S  = 0.10;
    private const int    WarmUpAttempts = 3;
    private const double MasteryThreshold = 0.90;
    private const int    ConsecutiveCorrectForMastery = 5;

    public BktService(OdinDbContext db) { _db = db; }

    public async Task<BktResult> UpdateMasteryAsync(Guid userId, string topic, bool isCorrect)
    {
        var mastery = await _db.MasteryStates
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Topic == topic);

        if (mastery == null)
        {
            mastery = new MasteryState
            {
                UserId = userId,
                Topic = topic,
                ProbabilityMastery = P_L0,
                MasteryPercentage = (int)(P_L0 * 100),
                IsLocked = false,
                AttemptCount = 0,
                ConsecutiveCorrect = 0
            };
            _db.MasteryStates.Add(mastery);
        }

        mastery.AttemptCount++;
        bool isWarmUp = mastery.AttemptCount <= WarmUpAttempts;

        if (!isWarmUp)
        {
            double pL = mastery.ProbabilityMastery;
            if (isCorrect)
            {
                double num = pL * (1.0 - P_S);
                double den = num + (1.0 - pL) * P_G;
                double pLGiven = den > 0 ? num / den : pL;
                mastery.ProbabilityMastery = pLGiven + (1.0 - pLGiven) * P_T;
            }
            else
            {
                double num = pL * P_S;
                double den = num + (1.0 - pL) * (1.0 - P_G);
                double pLGiven = den > 0 ? num / den : pL;
                mastery.ProbabilityMastery = pLGiven + (1.0 - pLGiven) * P_T;
            }
            mastery.ProbabilityMastery = Math.Clamp(mastery.ProbabilityMastery, 0.0, 1.0);
        }

        mastery.ConsecutiveCorrect = isCorrect ? mastery.ConsecutiveCorrect + 1 : 0;
        mastery.IsMastered = mastery.ProbabilityMastery >= MasteryThreshold
                          && mastery.ConsecutiveCorrect >= ConsecutiveCorrectForMastery;
        mastery.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new BktResult
        {
            ProbabilityMastery = mastery.ProbabilityMastery,
            IsMastered = mastery.IsMastered,
            IsWarmUpPhase = isWarmUp,
            AttemptCount = mastery.AttemptCount,
            ConsecutiveCorrect = mastery.ConsecutiveCorrect
        };
    }
}
