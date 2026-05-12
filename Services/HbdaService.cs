using System.Text.RegularExpressions;
using ODIN.Api.Models.Domain;
using ODIN.Api.Models.Enums;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Services;

/// <summary>
/// Heuristic Behavior Detection Algorithm (HBDA) — Stage 1 of the Sequential Pipeline.
/// Thresholds defined by the project psychologist.
///
/// Priority order (first match wins):
///   1. GamingTheSystem          — SI &lt; 2s OR paste-detected OR task &lt; 15s
///   2. PostFailureDisengagement — SI &gt;= 120s AND previous was error
///   3. WheelSpinning            — &gt;= 3 consecutive same errors, no structural change
///   4. LowProgressTrialAndError — SI &lt; 6s AND only numeric/operator swaps
///   5. HintWithheld             — SI &gt; 15s AND new/different error
///   6. ActiveThinking           — SI &gt; 15s AND correct/progressive AND &gt;= 2 consecutive progressive
/// </summary>
public class HbdaService : IHbdaService
{
    private const double GamingIntervalMax        = 2.0;   // SI < 2s
    private const double GamingTaskElapsedMin     = 15.0;  // task elapsed < 15s
    private const double DisengagementIntervalMin = 120.0; // SI >= 120s
    private const double LowProgressIntervalMax   = 6.0;   // SI < 6s
    private const double ThinkingIntervalMin      = 15.0;  // SI > 15s

    /// Max edit distance still considered "symbol cycling" for LPTAE.
    /// Above this threshold the student is making substantive edits even if
    /// the normalised code structure looks the same.
    private const int LowProgressMaxEditDistance = 25;

    private const double WeightGaming               = 20.0;
    private const double WeightDisengagement        = 15.0;
    private const double WeightWheelSpinning        = 15.0;
    private const double WeightLowProgress          = 10.0;
    private const double WeightHintWithheld         = -5.0;
    private const double WeightActiveThinking       = -10.0;

    public HbdaResult Classify(
        CodeSubmission current,
        CodeSubmission? previous,
        List<CodeSubmission> sessionHistory,
        double inactivityDuration)
    {
        var gamingCheck = IsGamingTheSystem(current, sessionHistory);
        if (gamingCheck.IsGaming)
            return Result(BehaviorState.GamingTheSystem, WeightGaming * gamingCheck.Confidence, gamingCheck.Reason);

        if (IsPostFailureDisengagement(current, previous, inactivityDuration))
            return Result(BehaviorState.PostFailureDisengagement, WeightDisengagement,
                $"PostFailureDisengagement: inactivity={inactivityDuration:F1}s after error");

        if (IsWheelSpinning(current, previous, sessionHistory))
            return Result(BehaviorState.WheelSpinning, WeightWheelSpinning,
                $"WheelSpinning: >=3 consecutive {current.DiagnosticCategory} errors, no structural change");

        if (IsLowProgressTrialAndError(current, previous))
            return Result(BehaviorState.LowProgressTrialAndError, WeightLowProgress,
                $"LowProgressTrialAndError: SI={current.SubmissionIntervalSeconds:F1}s, only symbol swaps");

        if (IsHintWithheld(current, previous))
            return Result(BehaviorState.HintWithheld, WeightHintWithheld,
                $"HintWithheld: SI={current.SubmissionIntervalSeconds:F1}s, new error type");

        if (IsActiveThinking(current, previous, sessionHistory))
            return Result(BehaviorState.ActiveThinking, WeightActiveThinking,
                $"ActiveThinking: SI={current.SubmissionIntervalSeconds:F1}s, progressive/correct, >= 2 consecutive");

        return Result(BehaviorState.LowProgressTrialAndError, WeightLowProgress * 0.5,
            "No clear pattern — mild low-progress default");
    }

    // ═══════════════════════════════════════════════════════════
    // Detectors
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Gaming the System detection with confidence scores.
    /// </summary>
    private static (bool IsGaming, double Confidence, string Reason) IsGamingTheSystem(CodeSubmission c, List<CodeSubmission> history)
    {
        // Must exclude: Paste actions used to copy the student's own prior work
        bool isPasteOfPriorWork = false;
        if (c.PasteDetected && history.Count > 0)
        {
            string currentNorm = NormalizeStructure(c.SourceCode);
            isPasteOfPriorWork = history.Any(h => NormalizeStructure(h.SourceCode) == currentNorm);
        }

        bool actualPasteDetected = c.PasteDetected && !isPasteOfPriorWork;

        // High Confidence (1.0)
        if (actualPasteDetected || c.TaskElapsedSeconds < GamingTaskElapsedMin || c.SubmissionIntervalSeconds < GamingIntervalMax)
        {
            return (true, 1.0, $"High Confidence Gaming: paste={actualPasteDetected}, task={c.TaskElapsedSeconds:F0}s, SI={c.SubmissionIntervalSeconds:F1}s");
        }

        // Moderate Confidence (0.75): Bulk paste followed by minor symbol adjustments
        var lastSub = history.OrderByDescending(h => h.SubmittedAt).FirstOrDefault();
        if (lastSub != null && lastSub.PasteDetected && c.EditDistance <= 25 && c.EditDistance > 0)
        {
            return (true, 0.75, $"Moderate Confidence Gaming: Minor adjustments after paste. ED={c.EditDistance}");
        }

        // Low Confidence (0.5): Excessive hint requests (>3 in 60s)
        if (c.HintUsageCount > 3 && c.TaskElapsedSeconds < 60.0)
        {
            return (true, 0.5, $"Low Confidence Gaming: Excessive hints ({c.HintUsageCount}) in {c.TaskElapsedSeconds:F0}s");
        }

        return (false, 0.0, "");
    }

    /// PostFailureDisengagement: keyboard idle >= 120s immediately after an error
    private static bool IsPostFailureDisengagement(CodeSubmission c, CodeSubmission? prev, double inactivityDuration)
    {
        if (inactivityDuration < DisengagementIntervalMin) return false;
        if (c.IsCorrect) return false;
        // Previous submission must also have been an error
        return prev != null && !prev.IsCorrect;
    }

    /// WheelSpinning: >= 3 consecutive submissions share the same DiagnosticCategory AND no structural change
    private static bool IsWheelSpinning(CodeSubmission c, CodeSubmission? prev, List<CodeSubmission> history)
    {
        if (prev == null || history.Count < 2) return false;
        if (c.IsCorrect) return false;

        // Last 2 in history + current must all have the same diagnostic category
        var last2 = history.OrderByDescending(h => h.SubmittedAt).Take(2).ToList();
        bool sameError = last2.All(h => h.DiagnosticCategory == c.DiagnosticCategory)
                         && c.DiagnosticCategory != "None";
        if (!sameError) return false;

        // No structural change between current and previous
        return NormalizeStructure(c.SourceCode) == NormalizeStructure(prev.SourceCode);
    }

    /// LowProgressTrialAndError: SI < 6s AND only numeric/operator swaps (no structural change)
    /// EditDistance must be small (<= 25) — large edits indicate genuine rework, not symbol cycling.
    private static bool IsLowProgressTrialAndError(CodeSubmission c, CodeSubmission? prev)
    {
        if (c.SubmissionIntervalSeconds >= LowProgressIntervalMax) return false;
        if (c.IsCorrect) return false;
        if (prev == null) return false;

        // Substantive rewrites disqualify LPTAE even if normalised structure matches.
        // e.g. moving code to a different line changes edit distance significantly
        // but can produce the same normalised form.
        if (c.EditDistance > LowProgressMaxEditDistance) return false;

        // Source changed (ED > 0) but only in numbers/operators
        return c.EditDistance > 0
               && NormalizeStructure(c.SourceCode) == NormalizeStructure(prev.SourceCode);
    }

    /// HintWithheld: SI > 15s AND a new/different error (student is actively exploring)
    private static bool IsHintWithheld(CodeSubmission c, CodeSubmission? prev)
    {
        if (c.SubmissionIntervalSeconds <= ThinkingIntervalMin) return false;
        if (c.IsCorrect) return false;
        if (prev == null) return true; // First attempt after a long pause = benefit of the doubt
        return c.DiagnosticCategory != prev.DiagnosticCategory;
    }

    /// ActiveThinking: SI > 15s AND (correct or progressive) AND >= 2 consecutive progressive
    private static bool IsActiveThinking(CodeSubmission c, CodeSubmission? prev, List<CodeSubmission> history)
    {
        if (c.SubmissionIntervalSeconds <= ThinkingIntervalMin) return false;

        bool currentProgressive = c.IsCorrect
            || (prev != null && c.DiagnosticCategory != prev.DiagnosticCategory);
        if (!currentProgressive) return false;

        // Previous submission must also have been progressive
        if (prev == null || history.Count < 2) return false;
        var beforePrev = history.OrderByDescending(h => h.SubmittedAt).Skip(1).FirstOrDefault();
        bool prevProgressive = prev.IsCorrect
            || (beforePrev != null && prev.DiagnosticCategory != beforePrev.DiagnosticCategory);

        return prevProgressive;
    }

    // ═══════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════

    /// Normalize code so only numeric literals and operator symbols can vary.
    /// Two codes with the same normalized form differ only in numbers/operators.
    private static string NormalizeStructure(string code)
    {
        code = Regex.Replace(code, @"//[^\r\n]*", "", RegexOptions.Multiline);
        code = Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline);
        code = Regex.Replace(code, @"\b\d+(\.\d+)?\b", "NUM");
        code = Regex.Replace(code, @"[+\-*/%=<>!&|^~?]", "OP");
        return Regex.Replace(code, @"\s+", " ").Trim();
    }

    private static HbdaResult Result(BehaviorState state, double delta, string reason) =>
        new() { State = state, HelplessnessScoreDelta = delta, Reasoning = reason };
}
