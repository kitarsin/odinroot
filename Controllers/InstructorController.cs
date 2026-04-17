using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ODIN.Api.Data;

namespace ODIN.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstructorController : ControllerBase
{
    private readonly OdinDbContext _db;
    public InstructorController(OdinDbContext db) { _db = db; }

    [HttpGet("overview")]
    public async Task<ActionResult> GetClassOverview()
    {
        var players = await _db.Players.ToListAsync();
        var logs = await _db.InteractionLogs.ToListAsync();
        var behaviorDist = logs.GroupBy(l => l.BehaviorState)
            .ToDictionary(g => g.Key, g => g.Count());

        return Ok(new
        {
            TotalStudents = players.Count,
            TotalSubmissions = players.Sum(p => p.TotalSubmissions),
            AverageHelplessnessScore = players.Count > 0
                ? Math.Round(players.Average(p => p.HelplessnessScore), 2) : 0,
            StudentsInDistress = players.Count(p => p.HelplessnessScore >= 50),
            BehaviorDistribution = behaviorDist
        });
    }

    [HttpGet("bottlenecks")]
    public async Task<ActionResult> GetCognitiveBottlenecks()
    {
        var bottlenecks = await _db.InteractionLogs
            .Where(l => l.DiagnosticCategory != "None")
            .GroupBy(l => new { l.DiagnosticCategory, l.SkillType })
            .Select(g => new
            {
                g.Key.DiagnosticCategory, g.Key.SkillType,
                Occurrences = g.Count(),
                AffectedStudents = g.Select(l => l.UserId).Distinct().Count(),
                AvgHelplessness = Math.Round(g.Average(l => l.CumulativeHelplessnessScore), 2)
            })
            .OrderByDescending(b => b.Occurrences)
            .ToListAsync();
        return Ok(bottlenecks);
    }

    [HttpGet("students")]
    public async Task<ActionResult> GetStudentList()
    {
        var students = await _db.Players
            .Include(p => p.MasteryStates)
            .OrderByDescending(p => p.HelplessnessScore)
            .Select(p => new
            {
                p.Id, p.StudentId, p.DisplayName, p.Section, p.CurrentLevel,
                p.HelplessnessScore, p.TotalSubmissions,
                OverallMastery = p.MasteryStates.Any()
                    ? Math.Round(p.MasteryStates.Average(m => m.ProbabilityMastery) * 100, 1) : 0,
                Status = p.HelplessnessScore >= 100 ? "CRITICAL"
                       : p.HelplessnessScore >= 50 ? "AT_RISK" : "STABLE"
            })
            .ToListAsync();
        return Ok(students);
    }

    [HttpGet("interventions")]
    public async Task<ActionResult> GetRecentInterventions([FromQuery] int limit = 50)
    {
        var interventions = await _db.InteractionLogs
            .Where(l => l.InterventionTriggered != "None")
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .Join(_db.Players, log => log.UserId, player => player.Id,
                (log, player) => new
                {
                    log.Timestamp, player.StudentId, player.DisplayName,
                    log.BehaviorState, log.InterventionTriggered, log.DiagnosticCategory,
                    log.SkillType, log.CumulativeHelplessnessScore, log.MasteryProbability
                })
            .ToListAsync();
        return Ok(interventions);
    }

    [HttpGet("mastery-heatmap")]
    public async Task<ActionResult> GetMasteryHeatmap()
    {
        var heatmap = await _db.MasteryStates
            .GroupBy(m => m.Topic)
            .Select(g => new
            {
                Skill = g.Key,
                AverageMastery = Math.Round(g.Average(m => m.ProbabilityMastery) * 100, 1),
                StudentsAttempted = g.Count(m => m.AttemptCount > 0),
                StudentsMastered = g.Count(m => m.IsMastered),
                AverageAttempts = Math.Round(g.Average(m => (double)m.AttemptCount), 1)
            })
            .ToListAsync();
        return Ok(heatmap);
    }
}
