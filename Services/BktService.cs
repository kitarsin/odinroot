using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ODIN.Api.Data;
using ODIN.Api.Models.Domain;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Services;

public class BktService : IBktService
{
    private readonly OdinDbContext _db;
    private static readonly ConcurrentDictionary<string, ArenaBktState> ArenaStates = new();
    private static readonly TimeSpan ArenaStateTtl = TimeSpan.FromHours(6);

    private const double P_L0 = 0.10;
    private const double P_T  = 0.10;
    private const double P_G  = 0.20;
    private const double P_S  = 0.10;
    private const int    WarmUpAttempts = 3;
    private const double MasteryThreshold = 0.90;
    private const int    ConsecutiveCorrectForMastery = 5;
    private const double UncertaintyThreshold  = 0.40;  // P(L) below this = uncertain mastery
    private const int    HelplessTransitions   = 5;     // consecutive uncertain transitions to confirm Helpless

    public BktService(OdinDbContext db) { _db = db; }

    public async Task<BktResult> UpdateMasteryAsync(Guid userId, int dungeonLevel, bool isCorrect)
    {
        var mastery = await _db.MasteryStates
            .FirstOrDefaultAsync(m => m.UserId == userId && m.DungeonLevel == dungeonLevel);

        if (mastery == null)
        {
            mastery = new MasteryState
            {
                UserId = userId,
                DungeonLevel = dungeonLevel,
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
            mastery.MasteryPercentage  = (int)Math.Round(mastery.ProbabilityMastery * 100);

            // Track consecutive transitions that remain below the Uncertainty Threshold.
            // When this count reaches HelplessTransitions, the BKT engine signals a Helpless state.
            if (mastery.ProbabilityMastery < UncertaintyThreshold)
                mastery.ConsecutiveLowProbability++;
            else
                mastery.ConsecutiveLowProbability = 0;
        }

        mastery.ConsecutiveCorrect = isCorrect ? mastery.ConsecutiveCorrect + 1 : 0;
        mastery.IsMastered = mastery.ProbabilityMastery >= MasteryThreshold
                          && mastery.ConsecutiveCorrect >= ConsecutiveCorrectForMastery;
        mastery.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        bool isHelpless = !isWarmUp && mastery.ConsecutiveLowProbability >= HelplessTransitions;

        return new BktResult
        {
            ProbabilityMastery        = mastery.ProbabilityMastery,
            MasteryPercentage         = mastery.MasteryPercentage,
            IsMastered                = mastery.IsMastered,
            IsWarmUpPhase             = isWarmUp,
            AttemptCount              = mastery.AttemptCount,
            ConsecutiveCorrect        = mastery.ConsecutiveCorrect,
            IsHelpless                = isHelpless,
            ConsecutiveLowProbability = mastery.ConsecutiveLowProbability
        };
    }

    public async Task<BktResult> PreviewArenaMasteryAsync(Guid userId, int dungeonLevel, string arenaRunId, bool isCorrect, bool forceWarmUpComplete)
    {
        if (string.IsNullOrWhiteSpace(arenaRunId))
            return await GetCurrentResultAsync(userId, dungeonLevel);

        var warmUpAttempts = forceWarmUpComplete ? 0 : WarmUpAttempts;
        PurgeExpiredArenaStates();
        var key = ArenaKey(userId, dungeonLevel, arenaRunId);
        if (!ArenaStates.TryGetValue(key, out var state))
        {
            state = CreateFreshArenaState();
            state = ArenaStates.GetOrAdd(key, state);
        }
        ApplyTransition(state, isCorrect, warmUpAttempts);
        state.LastTouchedAt = DateTime.UtcNow;
        return ToResult(state, warmUpAttempts);
    }

    public async Task<BktResult> CommitArenaMasteryAsync(Guid userId, int dungeonLevel, string arenaRunId)
    {
        if (string.IsNullOrWhiteSpace(arenaRunId))
            return await GetCurrentResultAsync(userId, dungeonLevel);

        var key = ArenaKey(userId, dungeonLevel, arenaRunId);
        if (!ArenaStates.TryRemove(key, out var state))
            return await GetCurrentResultAsync(userId, dungeonLevel);

        var mastery = await _db.MasteryStates
            .FirstOrDefaultAsync(m => m.UserId == userId && m.DungeonLevel == dungeonLevel);

        if (mastery == null)
        {
            mastery = new MasteryState
            {
                UserId = userId,
                DungeonLevel = dungeonLevel,
                IsLocked = false
            };
            _db.MasteryStates.Add(mastery);
        }

        mastery.ProbabilityMastery = state.ProbabilityMastery;
        mastery.MasteryPercentage = state.MasteryPercentage;
        mastery.AttemptCount = state.AttemptCount;
        mastery.ConsecutiveCorrect = state.ConsecutiveCorrect;
        mastery.ConsecutiveLowProbability = state.ConsecutiveLowProbability;
        mastery.IsMastered = state.IsMastered;
        mastery.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToResult(state, warmUpAttempts: 0);
    }

    private async Task<BktResult> GetCurrentResultAsync(Guid userId, int dungeonLevel)
    {
        var state = await LoadArenaState(userId, dungeonLevel);
        return ToResult(state);
    }

    private async Task<ArenaBktState> LoadArenaState(Guid userId, int dungeonLevel)
    {
        var mastery = await _db.MasteryStates
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.DungeonLevel == dungeonLevel);

        return mastery == null
            ? CreateFreshArenaState()
            : new ArenaBktState
            {
                ProbabilityMastery = mastery.ProbabilityMastery,
                MasteryPercentage = mastery.MasteryPercentage,
                AttemptCount = mastery.AttemptCount,
                ConsecutiveCorrect = mastery.ConsecutiveCorrect,
                ConsecutiveLowProbability = mastery.ConsecutiveLowProbability,
                IsMastered = mastery.IsMastered,
                LastTouchedAt = DateTime.UtcNow
            };
    }

    private static ArenaBktState CreateFreshArenaState() => new()
    {
        ProbabilityMastery = P_L0,
        MasteryPercentage = (int)(P_L0 * 100),
        AttemptCount = 0,
        ConsecutiveCorrect = 0,
        ConsecutiveLowProbability = 0,
        IsMastered = false,
        LastTouchedAt = DateTime.UtcNow
    };

    private static void ApplyTransition(ArenaBktState state, bool isCorrect, int warmUpAttempts = WarmUpAttempts)
    {
        state.AttemptCount++;
        var isWarmUp = state.AttemptCount <= warmUpAttempts;

        if (!isWarmUp)
        {
            var pL = state.ProbabilityMastery;
            if (isCorrect)
            {
                var num = pL * (1.0 - P_S);
                var den = num + (1.0 - pL) * P_G;
                var pLGiven = den > 0 ? num / den : pL;
                state.ProbabilityMastery = pLGiven + (1.0 - pLGiven) * P_T;
            }
            else
            {
                var num = pL * P_S;
                var den = num + (1.0 - pL) * (1.0 - P_G);
                var pLGiven = den > 0 ? num / den : pL;
                state.ProbabilityMastery = pLGiven + (1.0 - pLGiven) * P_T;
            }
            state.ProbabilityMastery = Math.Clamp(state.ProbabilityMastery, 0.0, 1.0);
            state.MasteryPercentage = (int)Math.Round(state.ProbabilityMastery * 100);

            if (state.ProbabilityMastery < UncertaintyThreshold)
                state.ConsecutiveLowProbability++;
            else
                state.ConsecutiveLowProbability = 0;
        }

        state.ConsecutiveCorrect = isCorrect ? state.ConsecutiveCorrect + 1 : 0;
        state.IsMastered = state.ProbabilityMastery >= MasteryThreshold
                         && state.ConsecutiveCorrect >= ConsecutiveCorrectForMastery;
    }

    private static BktResult ToResult(ArenaBktState state, int warmUpAttempts = WarmUpAttempts)
    {
        var isWarmUp = state.AttemptCount <= warmUpAttempts;
        return new BktResult
        {
            ProbabilityMastery = state.ProbabilityMastery,
            MasteryPercentage = state.MasteryPercentage,
            IsMastered = state.IsMastered,
            IsWarmUpPhase = isWarmUp,
            AttemptCount = state.AttemptCount,
            ConsecutiveCorrect = state.ConsecutiveCorrect,
            IsHelpless = !isWarmUp && state.ConsecutiveLowProbability >= HelplessTransitions,
            ConsecutiveLowProbability = state.ConsecutiveLowProbability
        };
    }

    private static string ArenaKey(Guid userId, int dungeonLevel, string arenaRunId) =>
        $"{userId:N}:{dungeonLevel}:{arenaRunId}";

    private static void PurgeExpiredArenaStates()
    {
        var cutoff = DateTime.UtcNow - ArenaStateTtl;
        foreach (var pair in ArenaStates)
        {
            if (pair.Value.LastTouchedAt < cutoff)
                ArenaStates.TryRemove(pair.Key, out _);
        }
    }

    private sealed class ArenaBktState
    {
        public double ProbabilityMastery { get; set; }
        public int MasteryPercentage { get; set; }
        public int AttemptCount { get; set; }
        public int ConsecutiveCorrect { get; set; }
        public int ConsecutiveLowProbability { get; set; }
        public bool IsMastered { get; set; }
        public DateTime LastTouchedAt { get; set; }
    }
}
