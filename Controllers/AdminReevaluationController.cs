using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ODIN.Api.Data;
using ODIN.Api.Models.Domain;
using ODIN.Api.Models.Enums;
using ODIN.Api.Services.Interfaces;
using System.Text.Json;

namespace ODIN.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminReevaluationController : ControllerBase
{
    private readonly OdinDbContext _db;
    private readonly IBktService _bktService;
    private readonly IAffectiveStateService _affectiveState;

    // Highest-confidence weights from HbdaService
    private const double WeightGaming = 20.0;
    private const double WeightDisengagementHigh = 20.0;
    private const double WeightWheelSpinningHigh = 15.0;
    private const double WeightTinkeringHigh = 10.0;
    private const double WeightHintWithheld = -5.0;
    private const double WeightActiveThinkingHigh = -20.0;

    public AdminReevaluationController(
        OdinDbContext db,
        IBktService bktService,
        IAffectiveStateService affectiveState)
    {
        _db = db;
        _bktService = bktService;
        _affectiveState = affectiveState;
    }

    [HttpPost("reevaluate")]
    public async Task<IActionResult> ReevaluateDatabase()
    {
        string logPath = @"c:\RUSSDM\Projects\Godot\ODIN_Website\all_game_logs.json";
        if (!System.IO.File.Exists(logPath))
        {
            return NotFound(new { error = $"Log file not found at {logPath}" });
        }

        try
        {
            string rawJson = await System.IO.File.ReadAllTextAsync(logPath);
            // Clean // Source: 
            var cleanJson = System.Text.RegularExpressions.Regex.Replace(rawJson, @"^\s*//.*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            
            using var document = JsonDocument.Parse(cleanJson);
            
            // 1. Wipe Mastery and InteractionLogs, and reset HelplessnessScore
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM progress");
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM interaction_logs");
            await _db.Database.ExecuteSqlRawAsync("UPDATE profiles SET helplessness_score = 0");

            int submissionsUpdated = 0;

            foreach (var studentNode in document.RootElement.EnumerateArray())
            {
                if (!studentNode.TryGetProperty("sessions", out var sessionsNode)) continue;

                foreach (var sessionNode in sessionsNode.EnumerateArray())
                {
                    if (!sessionNode.TryGetProperty("submissions", out var subsNode)) continue;

                    foreach (var subNode in subsNode.EnumerateArray())
                    {
                        var subIdStr = subNode.GetProperty("id").GetString();
                        if (string.IsNullOrEmpty(subIdStr) || !Guid.TryParse(subIdStr, out var subId)) continue;

                        // Get from DB
                        var dbSub = await _db.CodeSubmissions.FirstOrDefaultAsync(s => s.Id == subId);
                        if (dbSub == null) continue;

                        // Update from JSON
                        string behaviorStr = subNode.GetProperty("behaviorState").GetString() ?? "LowProgressTrialAndError";
                        double interval = subNode.GetProperty("submissionIntervalSeconds").GetDouble();
                        double totalTime = subNode.GetProperty("totalTimeSeconds").GetDouble();

                        dbSub.BehaviorState = behaviorStr;
                        dbSub.SubmissionIntervalSeconds = interval;
                        dbSub.TotalTimeSeconds = totalTime;

                        // Convert to enum
                        if (!Enum.TryParse<BehaviorState>(behaviorStr, out var stateEnum))
                        {
                            stateEnum = BehaviorState.LowProgressTrialAndError;
                        }

                        // Determine Delta based on highest weights
                        double delta = stateEnum switch
                        {
                            BehaviorState.GamingTheSystem => WeightGaming,
                            BehaviorState.PostFailureDisengagement => WeightDisengagementHigh,
                            BehaviorState.WheelSpinning => WeightWheelSpinningHigh,
                            BehaviorState.LowProgressTrialAndError => WeightTinkeringHigh,
                            BehaviorState.HintWithheld => WeightHintWithheld,
                            BehaviorState.ActiveThinking => WeightActiveThinkingHigh,
                            _ => 0.0
                        };

                        var hbdaResult = new HbdaResult
                        {
                            State = stateEnum,
                            HelplessnessScoreDelta = delta
                        };

                        // Fetch player
                        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == dbSub.UserId);
                        if (player == null) continue;

                        // Re-run BKT
                        var bktResult = await _bktService.UpdateMasteryAsync(player.Id, dbSub.SkillType ?? "Unknown", dbSub.IsCorrect);

                        // Re-run Affective
                        var affectiveResult = _affectiveState.Evaluate(hbdaResult, bktResult, player.HelplessnessScore);
                        player.HelplessnessScore = affectiveResult.UpdatedHelplessnessScore;

                        // Add interaction log
                        var iLog = new InteractionLog
                        {
                            UserId = player.Id,
                            SubmissionId = dbSub.Id,
                            BehaviorState = behaviorStr,
                            HelplessnessScoreDelta = delta,
                            CumulativeHelplessnessScore = affectiveResult.UpdatedHelplessnessScore,
                            MasteryProbability = bktResult.ProbabilityMastery,
                            InterventionTriggered = dbSub.InterventionType ?? "None",
                            DiagnosticCategory = dbSub.DiagnosticCategory ?? "None",
                            SkillType = dbSub.SkillType ?? "Unknown",
                            Timestamp = dbSub.SubmittedAt
                        };
                        _db.InteractionLogs.Add(iLog);

                        submissionsUpdated++;
                    }
                }
            }

            await _db.SaveChangesAsync();

            return Ok(new { message = $"Successfully wiped state and re-evaluated {submissionsUpdated} submissions." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }
}
