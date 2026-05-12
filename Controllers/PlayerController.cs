using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ODIN.Api.Data;

namespace ODIN.Api.Controllers;

public record GameStateRequest(string Data);

[ApiController]
[Route("api/[controller]")]
public class PlayerController : ControllerBase
{
    private readonly OdinDbContext _db;
    public PlayerController(OdinDbContext db) { _db = db; }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult> GetProfile(Guid userId)
    {
        var player = await _db.Players
            .Include(p => p.MasteryStates)
            .FirstOrDefaultAsync(p => p.Id == userId);
        if (player == null) return NotFound();

        return Ok(new
        {
            player.Id, player.StudentId, player.DisplayName, player.Section,
            player.CurrentLevel, player.ExperiencePoints, player.HelplessnessScore,
            player.TotalSubmissions, player.CreatedAt,
            MasteryStates = player.MasteryStates.Select(m => new
            {
                m.Topic, m.ProbabilityMastery, m.MasteryPercentage,
                m.IsMastered, m.AttemptCount, m.ConsecutiveCorrect
            })
        });
    }

    [HttpGet("{userId:guid}/gamestate")]
    public async Task<ActionResult> GetGameState(Guid userId)
    {
        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == userId);
        if (player == null) return NotFound();
        return Ok(new { gameState = player.GameState });
    }

    [HttpPut("{userId:guid}/gamestate")]
    public async Task<ActionResult> PutGameState(Guid userId, [FromBody] GameStateRequest req)
    {
        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == userId);
        if (player == null) return NotFound();
        player.GameState = req.Data;
        player.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{userId:guid}/reset")]
    public async Task<ActionResult> ResetProgress(Guid userId)
    {
        if (!await _db.Players.AnyAsync(p => p.Id == userId))
            return NotFound();

        // Raw SQL in one explicit transaction — avoids EF ExecuteDelete/ExecuteUpdate not always
        // enlisting in Database.BeginTransactionAsync (Npgsql), and avoids loading huge game_state jsonb.
        // FK order: keystrokes → interaction_logs → submissions → game_sessions → progress → profiles.
        // Pretest: do not clear pretest_completed or touch pretest_responses (Supabase); students keep gate + responses.
        var utc = DateTime.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM public.keystroke_raw_events WHERE user_id = {userId}");
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM public.interaction_logs WHERE user_id = {userId}");
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM public.submissions WHERE user_id = {userId}");
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM public.game_sessions WHERE user_id = {userId}");
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM public.progress WHERE user_id = {userId}");

            var updated = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE public.profiles SET
                    current_level = 0,
                    experience_points = 0,
                    helplessness_score = 0,
                    total_submissions = 0,
                    game_state = '{{}}'::jsonb,
                    sync_rate = 0,
                    achievements = '[]'::jsonb,
                    updated_at = {utc}
                WHERE id = {userId}");
            if (updated != 1)
            {
                await tx.RollbackAsync();
                return NotFound();
            }

            await tx.CommitAsync();
            return NoContent();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [HttpGet("{userId:guid}/history")]
    public async Task<ActionResult> GetPlayerHistory(Guid userId, [FromQuery] int limit = 50)
    {
        var logs = await _db.InteractionLogs
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .Select(l => new
            {
                l.Id, l.Timestamp, l.BehaviorState, l.HelplessnessScoreDelta,
                l.CumulativeHelplessnessScore, l.MasteryProbability,
                l.InterventionTriggered, l.DiagnosticCategory, l.SkillType
            })
            .ToListAsync();
        return Ok(logs);
    }
}
