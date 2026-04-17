using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ODIN.Api.Data;
using ODIN.Api.Models.Domain;

namespace ODIN.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly OdinDbContext _db;
    public SessionController(OdinDbContext db) { _db = db; }

    [HttpPost]
    public async Task<ActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        var player = await _db.Players.FindAsync(request.UserId);
        if (player == null) return NotFound(new { error = "Player not found" });
        if (request.DungeonLevel > player.CurrentLevel)
            return BadRequest(new { error = "Dungeon level not yet unlocked", currentLevel = player.CurrentLevel });

        var session = new GameSession
        {
            UserId = request.UserId,
            DungeonLevel = request.DungeonLevel,
            PuzzleId = request.PuzzleId
        };
        _db.GameSessions.Add(session);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSession), new { id = session.Id }, new
        {
            session.Id, session.UserId, session.DungeonLevel, session.PuzzleId, session.StartedAt
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetSession(Guid id)
    {
        var session = await _db.GameSessions.FirstOrDefaultAsync(s => s.Id == id);
        if (session == null) return NotFound();
        return Ok(new
        {
            session.Id, session.UserId, session.DungeonLevel, session.PuzzleId,
            session.StartedAt, session.EndedAt, session.SubmissionCount, session.IsCompleted
        });
    }

    [HttpPatch("{id:guid}/end")]
    public async Task<ActionResult> EndSession(Guid id)
    {
        var session = await _db.GameSessions.FindAsync(id);
        if (session == null) return NotFound();
        session.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Session ended", session.Id, session.EndedAt });
    }

    [HttpGet("player/{userId:guid}")]
    public async Task<ActionResult> GetPlayerSessions(Guid userId, [FromQuery] int limit = 20)
    {
        var sessions = await _db.GameSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .Take(limit)
            .Select(s => new { s.Id, s.DungeonLevel, s.PuzzleId, s.StartedAt, s.EndedAt, s.SubmissionCount, s.IsCompleted })
            .ToListAsync();
        return Ok(sessions);
    }
}

public record CreateSessionRequest(Guid UserId, int DungeonLevel, string PuzzleId);
