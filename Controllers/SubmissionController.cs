using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
    private readonly ICodeExecutionService _codeExecution;
    private readonly IBktService _bkt;
    private readonly IAffectiveStateService _affectiveState;
    private readonly IInterventionController _interventionController;
    private readonly ILogger<SubmissionController> _logger;

    public SubmissionController(
        OdinDbContext db, IHbdaService hbda, IDiagnosticEngine diagnosticEngine,
        ICodeExecutionService codeExecution, IBktService bkt,
        IAffectiveStateService affectiveState,
        IInterventionController interventionController, ILogger<SubmissionController> logger)
    {
        _db = db; _hbda = hbda; _diagnosticEngine = diagnosticEngine;
        _codeExecution = codeExecution; _bkt = bkt; _affectiveState = affectiveState;
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

        // ── Fetch puzzle for starter code check + execution validation ──
        Puzzle? puzzle = null;
        if (Guid.TryParse(request.PuzzleId, out var puzzleGuid))
            puzzle = await _db.Puzzles.FindAsync(puzzleGuid);

        // ── Stage 1: AST Diagnosis ──
        var diagnosticResult = _diagnosticEngine.Diagnose(request.SourceCode, skillTypeEnum);

        // ── Starter Code Guard: block submissions identical to the starter template ──
        // Normalization strips comments and whitespace so trivial edits don't bypass.
        if (puzzle != null && NormalizeCode(request.SourceCode) == NormalizeCode(puzzle.StarterCode))
        {
            diagnosticResult.IsCorrect = false;
            diagnosticResult.Category  = DiagnosticCategory.UnchangedStarterCode;
            diagnosticResult.Message   = "Your code matches the provided starter template. Write your own solution — submitting the starting code unchanged won't count.";
        }

        // ── Stage 2: Execution-Based Output Validation ──
        // Only runs when:
        //   • Code wasn't flagged as unchanged starter (nothing to execute meaningfully)
        //   • No Roslyn compile errors (broken code can't run)
        //   • Puzzle has expected output recorded in the DB
        if (diagnosticResult.Category != DiagnosticCategory.UnchangedStarterCode
            && !diagnosticResult.CompilerDiagnostics.Any()
            && puzzle?.ExpectedOutput is { Length: > 0 } expectedOut)
        {
            var actualOutput = await _codeExecution.ExecuteAsync(request.SourceCode);

            if (actualOutput == null)
            {
                diagnosticResult.IsCorrect = false;
                diagnosticResult.Category  = DiagnosticCategory.GenericLogicError;
                diagnosticResult.Message   = "Your code could not be executed. Check for infinite loops, missing output, or unsupported operations.";
            }
            else if (_codeExecution.Normalize(actualOutput) != _codeExecution.Normalize(expectedOut))
            {
                diagnosticResult.IsCorrect = false;
                diagnosticResult.Category  = DiagnosticCategory.GenericLogicError;
                diagnosticResult.Message   = string.IsNullOrWhiteSpace(actualOutput)
                    ? "Your code produced no output. Make sure you have a Console.WriteLine with the correct value."
                    : "Your output does not match the expected result. Review your logic.";
            }
            else if (puzzle.TestCases is { Length: > 2 })
            {
                // Stage 3: Secondary anti-hardcoding tests.
                // Each test substitutes different data into the student's code and re-runs.
                // If the output stops matching after substitution, the student hardcoded the answer.
                var testCases = JsonSerializer.Deserialize<List<SecondaryTestCase>>(puzzle.TestCases);
                foreach (var tc in testCases ?? [])
                {
                    var altCode = Regex.Replace(request.SourceCode, tc.Find, tc.Replace);
                    if (altCode == request.SourceCode)
                    {
                        // Pattern not found — student removed the required data
                        diagnosticResult.IsCorrect = false;
                        diagnosticResult.Category  = DiagnosticCategory.GenericLogicError;
                        diagnosticResult.Message   = "Make sure your code uses the provided data — do not remove or hardcode the given values.";
                        break;
                    }
                    var altOutput = await _codeExecution.ExecuteAsync(altCode);
                    if (altOutput == null || _codeExecution.Normalize(altOutput) != _codeExecution.Normalize(tc.ExpectedOutput))
                    {
                        diagnosticResult.IsCorrect = false;
                        diagnosticResult.Category  = DiagnosticCategory.GenericLogicError;
                        diagnosticResult.Message   = "Your solution appears to hardcode the answer. Make sure it works correctly for any valid input.";
                        break;
                    }
                }
            }
        }

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

        // ── HBDA Classification ──
        var sessionHistory = await _db.CodeSubmissions
            .Where(s => s.SessionId == request.SessionId && s.UserId == request.PlayerId)
            .OrderBy(s => s.SubmittedAt)
            .ToListAsync();

        var hbdaResult = _hbda.Classify(submission, previousSubmission, sessionHistory);
        submission.BehaviorState = hbdaResult.State.ToString();

        // ── BKT Update ──
        var bktResult = await _bkt.UpdateMasteryAsync(
            request.PlayerId, request.SkillType, diagnosticResult.IsCorrect);

        // ── Affective State Evaluation ──
        var affectiveResult = _affectiveState.Evaluate(hbdaResult, bktResult, player.HelplessnessScore);
        player.HelplessnessScore = affectiveResult.UpdatedHelplessnessScore;
        player.TotalSubmissions++;
        player.UpdatedAt = DateTime.UtcNow;

        // ── Adaptive Intervention ──
        int currentHintTier = sessionHistory.Count(s => s.InterventionType == "ScaffoldingHint");

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

        // ── Achievement Detection ──
        var newAchievements = new List<string>();
        if (diagnosticResult.IsCorrect)
        {
            var existingNames = ParseAchievementNames(player.Achievements);

            // First Victory: no prior correct submission by this player
            if (!existingNames.Contains("First Victory"))
            {
                bool hadPrior = await _db.CodeSubmissions
                    .AnyAsync(s => s.UserId == request.PlayerId && s.IsCorrect && s.Id != submission.Id);
                if (!hadPrior)
                    newAchievements.Add("First Victory");
            }

            // Mastery-based — only meaningful when the submitted skill just reached mastery
            if (bktResult.IsMastered)
            {
                var mastery = await _db.MasteryStates
                    .Where(m => m.UserId == request.PlayerId)
                    .ToDictionaryAsync(m => m.Topic, m => m.IsMastered);

                bool Has(string s) => mastery.TryGetValue(s, out var v) && v;

                if (!existingNames.Contains("Array Master") && Has("ArrayInitialization") && Has("ArrayAccess"))
                    newAchievements.Add("Array Master");

                if (!existingNames.Contains("Loop Expert") && Has("ArrayIteration") && Has("ArrayOperations"))
                    newAchievements.Add("Loop Expert");

                if (!existingNames.Contains("2D Grid Expert") && Has("MultidimensionalArrays") && Has("JaggedArrays"))
                    newAchievements.Add("2D Grid Expert");

                if (!existingNames.Contains("Bug Slayer") &&
                    Has("ArrayInitialization") && Has("ArrayAccess") &&
                    Has("ArrayIteration") && Has("ArrayOperations") &&
                    Has("MultidimensionalArrays") && Has("JaggedArrays"))
                    newAchievements.Add("Bug Slayer");
            }
        }

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
            XpAwarded = interventionResult.XpAwarded,
            NewAchievements = newAchievements
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
        SkillType.ArrayIteration => 2,      SkillType.ArrayOperations => 2,
        SkillType.MultidimensionalArrays => 3, SkillType.JaggedArrays => 3,
        _ => 1
    };

    private static HashSet<string> ParseAchievementNames(string json)
    {
        try
        {
            if (JsonNode.Parse(json ?? "[]") is not JsonArray arr) return [];
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in arr)
                if (e?["name"]?.GetValue<string>() is { } n)
                    names.Add(n);
            return names;
        }
        catch { return []; }
    }

    // Strip comments and collapse whitespace so trivial edits don't bypass the starter code guard.
    private static string NormalizeCode(string code)
    {
        code = Regex.Replace(code, @"//[^\r\n]*", "", RegexOptions.Multiline);
        code = Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline);
        return Regex.Replace(code, @"\s+", " ").Trim();
    }
}
