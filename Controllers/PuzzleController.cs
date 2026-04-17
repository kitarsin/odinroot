using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ODIN.Api.Data;

namespace ODIN.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PuzzleController : ControllerBase
{
    private readonly OdinDbContext _db;
    public PuzzleController(OdinDbContext db) { _db = db; }

    [HttpGet("level/{level:int}")]
    public async Task<ActionResult> GetPuzzlesByLevel(int level)
    {
        if (level < 1 || level > 3)
            return BadRequest(new { error = "Invalid level. Must be 1, 2, or 3." });

        var puzzles = await _db.Puzzles
            .Where(p => p.DungeonLevel == level && p.IsActive)
            .OrderBy(p => p.OrderIndex)
            .Select(p => new { p.Id, p.Title, p.Description, p.DungeonLevel, p.OrderIndex, p.SkillType, p.StarterCode })
            .ToListAsync();
        return Ok(puzzles);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetPuzzle(Guid id)
    {
        var puzzle = await _db.Puzzles.FindAsync(id);
        if (puzzle == null) return NotFound();
        return Ok(new { puzzle.Id, puzzle.Title, puzzle.Description, puzzle.DungeonLevel, puzzle.SkillType, puzzle.StarterCode, puzzle.ExpectedOutput });
    }
}
