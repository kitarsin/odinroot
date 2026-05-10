using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ODIN.Api.Data;
using ODIN.Api.Models.Domain;
using ODIN.Api.Models.DTOs;
using ODIN.Api.Models.Enums;
using ODIN.Api.Services;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubmissionController : ControllerBase
{
    private readonly OdinDbContext _db;
    private readonly IHbdaService _hbda;
    private readonly IDiagnosticEngine _diagnosticEngine;
    private readonly IBktService _bkt;
    private readonly IAffectiveStateService _affectiveState;
    private readonly IInterventionController _interventionController;
    private readonly ILogger<SubmissionController> _logger;

    public SubmissionController(
        OdinDbContext db, IHbdaService hbda, IDiagnosticEngine diagnosticEngine,
        IBktService bkt, IAffectiveStateService affectiveState,
        IInterventionController interventionController, ILogger<SubmissionController> logger)
    {
        _db = db; _hbda = hbda; _diagnosticEngine = diagnosticEngine;
        _bkt = bkt; _affectiveState = affectiveState;
        _interventionController = interventionController; _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<SubmissionResponse>> Submit([FromBody] SubmissionRequest request)
    {
        var player = await _db.Players.FindAsync(request.PlayerId);
        if (player == null) return NotFound(new { error = "Player not found" });

        var session = await _db.GameSessions.FindAsync(request.SessionId);
        if (session == null) return NotFound(new { error = "Session not found" });

        if (!Enum.TryParse<SkillType>(request.SkillType, true, out var skillTypeEnum))
            return BadRequest(new { error = "Invalid SkillType", value = request.SkillType });

        // ── Stage 2: AST Diagnosis ──
        var diagnosticResult = _diagnosticEngine.Diagnose(request.SourceCode, skillTypeEnum);

        // ── Retrieve previous submission for edit distance ──
        var previousSubmission = await _db.CodeSubmissions
            .Where(s => s.SessionId == request.SessionId && s.UserId == request.PlayerId)
            .OrderByDescending(s => s.SubmittedAt)
            .FirstOrDefaultAsync();

        int editDistance = previousSubmission != null
            ? EditDistanceCalculator.Compute(previousSubmission.SourceCode, request.SourceCode)
            : request.SourceCode.Length;

        double submissionInterval = previousSubmission != null
            ? (DateTime.UtcNow - previousSubmission.SubmittedAt).TotalSeconds
            : request.KeystrokeData.TotalTimeSeconds;

        // ── Create submission record ──
        var submission = new CodeSubmission
        {
            UserId = request.PlayerId,
            SessionId = request.SessionId,
            QuestionId = request.PuzzleId,
            SourceCode = request.SourceCode,
            SkillType = request.SkillType,
            SubmittedAt = DateTime.UtcNow,
            AverageFlightTimeMs = request.KeystrokeData.AverageFlightTimeMs,
            AverageDwellTimeMs = request.KeystrokeData.AverageDwellTimeMs,
            InitialLatencyMs = request.KeystrokeData.InitialLatencyMs,
            TotalTimeSeconds = request.KeystrokeData.TotalTimeSeconds,
            EditDistance = editDistance,
            SubmissionIntervalSeconds = submissionInterval,
            HintUsageCount = request.HintUsageCount,
            IsCorrect = diagnosticResult.IsCorrect,
            DiagnosticCategory = diagnosticResult.Category.ToString(),
            DiagnosticMessage = diagnosticResult.Message
        };

        // ── Stage 1: HBDA Classification ──
        var sessionHistory = await _db.CodeSubmissions
            .Where(s => s.SessionId == request.SessionId && s.UserId == request.PlayerId)
            .OrderBy(s => s.SubmittedAt)
            .ToListAsync();

        var hbdaResult = _hbda.Classify(submission, previousSubmission, sessionHistory);
        submission.BehaviorState = hbdaResult.State.ToString();

        // ── Stage 3: BKT Update ──
        var bktResult = await _bkt.UpdateMasteryAsync(
            request.PlayerId, request.SkillType, diagnosticResult.IsCorrect);

        // ── Affective State Evaluation ──
        var affectiveResult = _affectiveState.Evaluate(hbdaResult, bktResult, player.HelplessnessScore);
        player.HelplessnessScore = affectiveResult.UpdatedHelplessnessScore;
        player.TotalSubmissions++;
        player.UpdatedAt = DateTime.UtcNow;

        // ── Intervention ──
        int currentHintTier = sessionHistory
            .Count(s => s.InterventionType == "ScaffoldingHint");

        var interventionResult = await _interventionController.DetermineInterventionAsync(
            hbdaResult.State, diagnosticResult, bktResult, affectiveResult,
            skillTypeEnum, currentHintTier);

        submission.InterventionType = interventionResult.Type.ToString();
        player.ExperiencePoints += interventionResult.XpAwarded;

        if (interventionResult.LevelUnlocked)
        {
            int nextLevel = GetDungeonLevelForSkill(skillTypeEnum) + 1;
            if (nextLevel > player.CurrentLevel && nextLevel <= 3)
                player.CurrentLevel = nextLevel;
        }

        _db.CodeSubmissions.Add(submission);
        session.SubmissionCount++;
        if (diagnosticResult.IsCorrect) session.IsCompleted = true;

        await _db.SaveChangesAsync();

        var interactionLog = new InteractionLog
        {
            UserId = request.PlayerId,
            SubmissionId = submission.Id,
            BehaviorState = hbdaResult.State.ToString(),
            HelplessnessScoreDelta = hbdaResult.HelplessnessScoreDelta,
            CumulativeHelplessnessScore = affectiveResult.UpdatedHelplessnessScore,
            MasteryProbability = bktResult.ProbabilityMastery,
            InterventionTriggered = interventionResult.Type.ToString(),
            DiagnosticCategory = diagnosticResult.Category.ToString(),
            SkillType = request.SkillType
        };
        _db.InteractionLogs.Add(interactionLog);

        // Persist raw keystroke events if the game client sent them
        if (request.KeystrokeData.RawEvents is { Count: > 0 } rawEvents)
        {
            _db.KeystrokeRawEventBatches.Add(new KeystrokeRawEventBatch
            {
                SubmissionId = submission.Id,
                UserId       = request.PlayerId,
                SessionId    = request.SessionId,
                Events       = JsonSerializer.Serialize(rawEvents)
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new SubmissionResponse
        {
            SubmissionId = submission.Id,
            IsCorrect = diagnosticResult.IsCorrect,
            DiagnosticCategory = diagnosticResult.Category.ToString(),
            DiagnosticMessage = diagnosticResult.Message,
            CompilerDiagnostics = diagnosticResult.CompilerDiagnostics,
            BehaviorState = hbdaResult.State.ToString(),
            HelplessnessScore = affectiveResult.UpdatedHelplessnessScore,
            HelplessnessScoreDelta = hbdaResult.HelplessnessScoreDelta,
            MasteryProbability = bktResult.ProbabilityMastery,
            IsMastered = bktResult.IsMastered,
            IsWarmUpPhase = bktResult.IsWarmUpPhase,
            InterventionType = interventionResult.Type.ToString(),
            NpcDialogue = interventionResult.NpcDialogue,
            LevelUnlocked = interventionResult.LevelUnlocked,
            XpAwarded = interventionResult.XpAwarded
        });
    }

    [HttpGet("session/{sessionId:guid}")]
    public async Task<ActionResult> GetSessionSubmissions(Guid sessionId)
    {
        var submissions = await _db.CodeSubmissions
            .Where(s => s.SessionId == sessionId)
            .OrderBy(s => s.SubmittedAt)
            .Select(s => new
            {
                s.Id, s.SubmittedAt, s.IsCorrect, s.SkillType,
                s.BehaviorState, s.DiagnosticCategory, s.DiagnosticMessage,
                s.InterventionType, s.AverageFlightTimeMs, s.AverageDwellTimeMs,
                s.InitialLatencyMs, s.TotalTimeSeconds, s.HintUsageCount,
                s.EditDistance, s.SubmissionIntervalSeconds
            })
            .ToListAsync();
        return Ok(submissions);
    }

    private static int GetDungeonLevelForSkill(SkillType skill) => skill switch
    {
        SkillType.ArrayInitialization => 1, SkillType.ArrayAccess => 1,
        SkillType.ArrayIteration => 2, SkillType.ArrayOperations => 2,
        SkillType.MultidimensionalArrays => 3, SkillType.JaggedArrays => 3,
        _ => 1
    };
}
