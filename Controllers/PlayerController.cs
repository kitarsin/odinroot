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
        var player = await _db.Players
            .Include(p => p.MasteryStates)
            .FirstOrDefaultAsync(p => p.Id == userId);
        if (player == null) return NotFound();

        // Delete in FK-safe order so no constraint violations
        var keystrokeBatches = await _db.KeystrokeRawEventBatches
            .Where(k => k.UserId == userId).ToListAsync();
        _db.KeystrokeRawEventBatches.RemoveRange(keystrokeBatches);

        var logs = await _db.InteractionLogs
            .Where(l => l.UserId == userId).ToListAsync();
        _db.InteractionLogs.RemoveRange(logs);

        var submissions = await _db.CodeSubmissions
            .Where(s => s.UserId == userId).ToListAsync();
        _db.CodeSubmissions.RemoveRange(submissions);

        var sessions = await _db.GameSessions
            .Where(s => s.UserId == userId).ToListAsync();
        _db.GameSessions.RemoveRange(sessions);

        _db.MasteryStates.RemoveRange(player.MasteryStates);

        // Reset all player stats
        player.CurrentLevel = 0;
        player.ExperiencePoints = 0;
        player.HelplessnessScore = 0;
        player.TotalSubmissions = 0;
        player.GameState = "{}";
        player.SyncRate = 0;
        player.Achievements = "[]";
        player.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
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
