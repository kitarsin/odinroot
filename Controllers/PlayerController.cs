using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ODIN.Api.Data;

namespace ODIN.Api.Controllers;

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
