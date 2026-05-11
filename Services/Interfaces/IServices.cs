using ODIN.Api.Models.Domain;
using ODIN.Api.Models.DTOs;
using ODIN.Api.Models.Enums;

namespace ODIN.Api.Services.Interfaces;

// ── Stage 1: HBDA ──
public interface IHbdaService
{
    HbdaResult Classify(
        CodeSubmission currentSubmission,
        CodeSubmission? previousSubmission,
        List<CodeSubmission> sessionHistory,
        double inactivityDuration);   // client-measured idle time (seconds)
}

public class HbdaResult
{
    public BehaviorState State { get; set; }
    public double HelplessnessScoreDelta { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

// ── Stage 2: AST Diagnostic Engine ──
public interface IDiagnosticEngine
{
    DiagnosticResult Diagnose(string sourceCode, SkillType skillType);
}

public class DiagnosticResult
{
    public bool IsCorrect { get; set; }
    public DiagnosticCategory Category { get; set; } = DiagnosticCategory.None;
    public string Message { get; set; } = string.Empty;
    public List<CompilerDiagnosticDto> CompilerDiagnostics { get; set; } = new();
}

// ── Stage 3: BKT Engine ──
public interface IBktService
{
    Task<BktResult> UpdateMasteryAsync(Guid userId, string topic, bool isCorrect);
}

public class BktResult
{
    public double ProbabilityMastery { get; set; }
    public bool IsMastered { get; set; }
    public bool IsWarmUpPhase { get; set; }
    public int AttemptCount { get; set; }
    public int ConsecutiveCorrect { get; set; }
}

// ── Affective State Evaluation ──
public interface IAffectiveStateService
{
    AffectiveResult Evaluate(HbdaResult hbdaResult, BktResult bktResult, double currentHelplessnessScore);
}

public class AffectiveResult
{
    public bool HelplessnessTriggered { get; set; }
    public double UpdatedHelplessnessScore { get; set; }
    public bool IsProductiveState { get; set; }
}

// ── Adaptive Intervention Controller ──
public interface IInterventionController
{
    Task<InterventionResult> DetermineInterventionAsync(
        BehaviorState behaviorState,
        DiagnosticResult diagnosticResult,
        BktResult bktResult,
        SkillType skillType,
        int currentHintTier,
        bool isFirstSubmission,
        string previousBehaviorState);  // empty string on first submission
}

public class InterventionResult
{
    public InterventionType Type { get; set; } = InterventionType.None;
    public NpcDialogueDto? NpcDialogue { get; set; }
    public bool LevelUnlocked { get; set; }
    public int XpAwarded { get; set; }
}
