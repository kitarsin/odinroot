using System.Text.RegularExpressions;
using ODIN.Api.Models.Domain;
using ODIN.Api.Models.Enums;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Services;

/// <summary>
/// Heuristic Behavior Detection Algorithm (HBDA) — Stage 1 of the Sequential Pipeline.
/// Classifies student submissions into behavioral states using thresholds
/// defined by the project psychologist.
///
/// Priority order (first match wins):
///   1. GamingTheSystem              — SI &lt; 2s OR paste-detected OR task &lt; 15s
///   2. PostFailureDisengagement     — Multiple indicators of learned helplessness
///   3. WheelSpinning                — &gt;= 3 consecutive same errors, no structural change
///   4. LowProgressTrialAndError     — SI &lt; 6s with character/value cycling or persistent error
///   5. HintWithheld                 — SI &gt; 15s AND new/different error
///   6. ActiveThinking               — SI &gt; 15s AND correct/progressive AND &gt;= 2 consecutive progressive
///
/// Learned Helplessness Detection:
///   - Indicator #1: Inactivity >= 120s after error
///   - Indicator #2: Trivial edit (ED &lt;= 5) after error
///   - Indicator #3: 2+ identical submissions (ED == 0) with no edit between
///
/// Tinkering (Low-Progress Trial-and-Error):
///   - Indicator #1: SI &lt; 6s rapid submissions
///   - Indicator #2: Same character cycled 3+ times (delete/retype pattern)
///   - Indicator #3: Single-digit value swapped 3+ times
///   - Indicator #4: Persistent error (same DiagnosticCategory 3+ times)
///   - Indicator #5: No structural change (normalized structure identical)
///
/// Confidence Levels:
///   - HIGH: Multiple indicators or explicit time/cycle proof
///   - MODERATE: Single strong indicator without corroboration
///   - LOW: Weak evidence requiring monitoring
/// </summary>
public class HbdaService : IHbdaService
{
    // ─── Gaming Detection ───
    private const double GamingIntervalMax        = 2.0;   // SI < 2s
    private const double GamingTaskElapsedMin     = 15.0;  // task elapsed < 15s
    
    // ─── Disengagement Detection (Learned Helplessness) ───
    private const double DisengagementIntervalMin       = 120.0;  // Inactivity >= 120s
    private const int    UnchangedResubmissionMaxEd     = 5;      // EditDistance <= 5 still "unchanged"
    private const int    IdenticalSubmissionThreshold   = 2;      // 2+ identical submissions = pattern
    
    // ─── Tinkering Detection ───
    private const double TinkeringIntervalMax     = 6.0;   // SI < 6s (rapid)
    private const int    CharacterCycleThreshold  = 3;     // Same character cycled 3+ times
    private const int    NumericCycleThreshold    = 3;     // Numeric value cycled 3+ times
    private const int    PersistentErrorThreshold = 3;     // Same error 3+ times
    private const int    StrategicChangeThreshold = 10;    // ED >= 10 indicates significant rework
    
    // ─── Other Behaviors ───
    private const double ThinkingIntervalMin      = 15.0;  // SI > 15s
    private const double ReflectionPauseMin       = 15.0;  // Pause > 15s indicates reflection
    private const int    LowProgressMaxEditDistance = 25;   // Max ED for symbol cycling

    // ─── Active Thinking Detection ───
    private const double PreTaskPauseMin          = 10.0;  // Initial latency >= 10s
    private const double TypingBurstCoverageMin   = 0.60;  // >= 60% task coverage
    private const int    MaxSystemChecks          = 1;     // <= 1 system checks

    // ─── Weights (Helplessness Score Delta) ───
    private const double WeightGaming               = 20.0;
    private const double WeightDisengagementHigh    = 20.0;   // Multiple indicators or 120s+ proof
    private const double WeightDisengagementModerate = 12.0;  // 60% weight for single indicator
    private const double WeightDisengagementLow      = 8.0;   // 40% weight for weak evidence
    private const double WeightWheelSpinning         = 15.0;
    private const double WeightTinkeringHigh         = 10.0;  // Multiple rapid/cycling indicators
    private const double WeightTinkeringModerate     = 6.0;   // Single indicator (60% weight)
    private const double WeightTinkeringLow          = 3.0;   // Weak evidence (30% weight)
    private const double WeightHintWithheld          = -5.0;
    
    private const double WeightActiveThinkingHigh     = -20.0;
    private const double WeightActiveThinkingModerate = -12.0;
    private const double WeightActiveThinkingLow      = -8.0;


    public HbdaResult Classify(
        CodeSubmission current,
        CodeSubmission? previous,
        List<CodeSubmission> sessionHistory,
        double inactivityDuration)
    {
        var gamingCheck = IsGamingTheSystem(current, sessionHistory);
        if (gamingCheck.IsGaming)
            return Result(BehaviorState.GamingTheSystem, WeightGaming * gamingCheck.Confidence, gamingCheck.Reason);

        // Enhanced PostFailureDisengagement detection with multiple indicators
        var disengagementResult = EvaluatePostFailureDisengagement(current, previous, sessionHistory, inactivityDuration);
        if (disengagementResult != null)
            return disengagementResult;

        if (IsWheelSpinning(current, previous, sessionHistory))
            return Result(BehaviorState.WheelSpinning, WeightWheelSpinning, ConfidenceLevel.High,
                $"WheelSpinning: >=3 consecutive {current.DiagnosticCategory} errors, no structural change");

        // Enhanced LowProgressTrialAndError (Tinkering) detection with multiple indicators
        var tinkeringResult = EvaluateTinkering(current, previous, sessionHistory);
        if (tinkeringResult != null)
            return tinkeringResult;

        if (IsHintWithheld(current, previous))
            return Result(BehaviorState.HintWithheld, WeightHintWithheld, ConfidenceLevel.Moderate,
                $"HintWithheld: SI={current.SubmissionIntervalSeconds:F1}s, new error type");

        // Enhanced ActiveThinking detection with multiple indicators
        var thinkingResult = EvaluateActiveThinking(current, previous, sessionHistory);
        if (thinkingResult != null)
            return thinkingResult;

        return Result(BehaviorState.LowProgressTrialAndError, WeightTinkeringLow * 0.5, ConfidenceLevel.Low,
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
        // Exclusion #1: Brief pause (< 120s) even if ED is small = reflection, not helplessness
        if (inactivityDuration < DisengagementIntervalMin)
        {
            // Allow reflection even with small edits if new error type (exploration)
            if (previous != null && current.DiagnosticCategory != previous.DiagnosticCategory)
                return true;
        }

        // Exclusion #2: Even if 120s+ pause, if followed by major structural change = strategy pivot
        if (inactivityDuration >= DisengagementIntervalMin && current.EditDistance >= 10)
        {
            // Check if error type changed (indicating new approach)
            if (previous != null && current.DiagnosticCategory != previous.DiagnosticCategory)
            {
                // Check if session shows recovery (any successful submissions afterward)
                bool hasRecoveryInSession = sessionHistory.Any(s => s.IsCorrect);
                if (hasRecoveryInSession)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Evaluates confidence level and returns appropriate weight for PostFailureDisengagement.
    /// 
    /// HIGH: Multiple indicators OR explicit 120s+ inactivity
    /// MODERATE: Single indicator without time confirmation (ED or identical repeated)
    /// LOW: Weak evidence requiring monitoring
    /// </summary>
    private static (ConfidenceLevel, double) EvaluateLearnedHelplessnessConfidence(
        bool hasInactivity120Plus,
        bool hasUnchangedEdit,
        bool hasIdenticalRepeated)
    {
        int indicatorCount = (hasInactivity120Plus ? 1 : 0) 
                           + (hasUnchangedEdit ? 1 : 0) 
                           + (hasIdenticalRepeated ? 1 : 0);

        // HIGH: Multiple indicators OR explicit long inactivity
        if (indicatorCount >= 2 || hasInactivity120Plus)
            return (ConfidenceLevel.High, WeightDisengagementHigh);

        // MODERATE: Single indicator without inactivity proof
        if (indicatorCount == 1 && (hasUnchangedEdit || hasIdenticalRepeated))
            return (ConfidenceLevel.Moderate, WeightDisengagementModerate);

        // LOW: Weak or incomplete evidence
        return (ConfidenceLevel.Low, WeightDisengagementLow);
    }

    /// <summary>Helper to generate human-readable summary of which indicators triggered.</summary>
    private static string GetIndicatorSummary(bool ind1, bool ind2, bool ind3)
    {
        var parts = new List<string>();
        if (ind1) parts.Add("Inactivity>=120s");
        if (ind2) parts.Add("UnchangedEdit(ED<=5)");
        if (ind3) parts.Add("IdenticalRepeated(2+)");
        return string.Join(" + ", parts);
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

    /// <summary>
    /// Comprehensive Tinkering (Low-Progress Trial-and-Error) detection.
    /// Evaluates multiple indicators:
    ///   1. Rapid submission interval (SI < 6s)
    ///   2. Repeated character cycling (same char delete/retype 3+ times)
    ///   3. Single-digit value swapping (numeric value cycled 3+ times)
    ///   4. Persistent error pattern (same error 3+ times)
    ///   5. No structural change (normalized structure identical)
    ///
    /// Returns null if no tinkering detected.
    /// Applies exclusion logic for productive corrections and strategic progressions.
    /// </summary>
    private HbdaResult? EvaluateTinkering(
        CodeSubmission current,
        CodeSubmission? previous,
        List<CodeSubmission> sessionHistory)
    {
        // Only applies to unsuccessful submissions
        if (current.IsCorrect) return null;
        
        // Exclusion #1: Deliberate symbol correction that succeeded
        // Already caught by IsCorrect check above, but keep logic separate for clarity

        // Collect evidence of indicators
        bool indicator1_RapidSI = current.SubmissionIntervalSeconds < TinkeringIntervalMax;
        bool indicator2_CharacterCycling = IsRepeatedCharacterCycling(current, previous, sessionHistory);
        bool indicator3_NumericCycling = IsNumericValueCycling(current, previous, sessionHistory);
        bool indicator4_PersistentError = IsPersistentErrorPattern(current, previous, sessionHistory);
        bool indicator5_NoStructuralChange = previous != null && 
                                              NormalizeStructure(current.SourceCode) == NormalizeStructure(previous.SourceCode);

        // If no indicators present, not tinkering
        if (!indicator1_RapidSI && !indicator2_CharacterCycling && !indicator3_NumericCycling 
            && !indicator4_PersistentError)
            return null;

        // Exclusion #2: Strategic element progression (different error types = exploring)
        if (ShouldExcludeAsStrategicProgression(current, previous, sessionHistory))
            return null;

        // Exclusion #3: Reflection pause + recovery (pause > 15s then major change)
        if (ShouldExcludeAsReflectionRecovery(current, previous, sessionHistory))
            return null;

        // Determine confidence based on indicator strength and count
        var (confidenceLevel, delta) = EvaluateTinkeringConfidence(
            indicator1_RapidSI, indicator2_CharacterCycling, indicator3_NumericCycling, 
            indicator4_PersistentError, indicator5_NoStructuralChange);

        var indicatorSummary = GetTinkeringIndicatorSummary(
            indicator1_RapidSI, indicator2_CharacterCycling, indicator3_NumericCycling, 
            indicator4_PersistentError, indicator5_NoStructuralChange);

        return Result(
            BehaviorState.LowProgressTrialAndError,
            delta,
            confidenceLevel,
            $"Tinkering ({confidenceLevel}): {indicatorSummary}");
    }

    /// <summary>
    /// Detects Indicator #2: Same character deleted and retyped 3+ times in rapid succession.
    /// Tracks if character at a specific position cycles between states across consecutive submissions.
    /// </summary>
    private static bool IsRepeatedCharacterCycling(
        CodeSubmission current,
        CodeSubmission? previous,
        List<CodeSubmission> history)
    {
        if (previous == null || history.Count < 2) return false;
        if (current.EditDistance <= 0 || current.EditDistance > UnchangedResubmissionMaxEd) return false;

        // Simple heuristic: if ED is small and structures are same, likely symbol cycling
        // Look for alternating patterns like == vs = or i vs int i
        // Full implementation would track character-level changes across submission history
        // For now, detect the pattern via normalized structure match + small ED + rapid interval
        
        bool rapidInterval = current.SubmissionIntervalSeconds < TinkeringIntervalMax;
        bool normalizedMatch = NormalizeStructure(current.SourceCode) == NormalizeStructure(previous.SourceCode);
        
        // Check if this is part of a cycling pattern (look back at recent submissions)
        if (rapidInterval && normalizedMatch && history.Count >= 2)
        {
            // Check last 3 submissions for cycling pattern (ED always small, normalized same)
            var recent = history.OrderByDescending(h => h.SubmittedAt).Take(3).ToList();
            int cyclingCount = 0;
            
            foreach (var submission in recent)
            {
                if (submission.EditDistance > 0 && submission.EditDistance <= UnchangedResubmissionMaxEd)
                {
                    cyclingCount++;
                }
            }
            
            // If at least 3 recent submissions with small ED + rapid intervals, likely cycling
            return cyclingCount >= 2; // 2 in history + current = 3 total
        }

        return false;
    }

    /// <summary>
    /// Detects Indicator #3: Single-digit value cycled through multiple values.
    /// Tracks numeric literal changes across rapid submissions.
    /// </summary>
    private static bool IsNumericValueCycling(
        CodeSubmission current,
        CodeSubmission? previous,
        List<CodeSubmission> history)
    {
        if (previous == null || history.Count < 2) return false;
        if (current.EditDistance <= 0 || current.EditDistance > UnchangedResubmissionMaxEd) return false;

        // Extract all numeric values from current and previous code
        var currentNumbers = ExtractNumericLiterals(current.SourceCode);
        var previousNumbers = ExtractNumericLiterals(previous.SourceCode);

        // Check if only numeric values changed (rest of structure same)
        if (currentNumbers.Count == previousNumbers.Count && 
            currentNumbers.Count > 0 &&
            NormalizeStructure(current.SourceCode) == NormalizeStructure(previous.SourceCode))
        {
            // Numbers differ
            bool onlyNumbersChanged = currentNumbers.SequenceEqual(previousNumbers) == false;
            if (!onlyNumbersChanged) return false;

            // Check for cycling pattern in recent submissions
            var recent = history.OrderByDescending(h => h.SubmittedAt).Take(3).ToList();
            var numberSequences = recent.Select(s => ExtractNumericLiterals(s.SourceCode)).ToList();
            
            // If we have 3 submissions with different numeric values but same structure = cycling
            bool hasDifferentNumbers = numberSequences.Skip(1)
                .Any(nums => !nums.SequenceEqual(numberSequences.First()));
            
            return hasDifferentNumbers;
        }

        return false;
    }

    /// <summary>
    /// Detects Indicator #4: Persistent error pattern (same error 3+ times).
    /// </summary>
    private static bool IsPersistentErrorPattern(
        CodeSubmission current,
        CodeSubmission? previous,
        List<CodeSubmission> history)
    {
        if (current.IsCorrect || current.DiagnosticCategory == "None") return false;
        if (previous == null || !previous.Equals(current.DiagnosticCategory)) 
        {
            if (previous?.DiagnosticCategory != current.DiagnosticCategory) return false;
        }

        // Count consecutive submissions with same error
        int sameErrorCount = 1; // Current submission
        foreach (var submission in history.OrderByDescending(h => h.SubmittedAt))
        {
            if (submission.DiagnosticCategory == current.DiagnosticCategory && !submission.IsCorrect)
            {
                sameErrorCount++;
            }
            else
            {
                break; // Stop at first different error
            }
        }

        return sameErrorCount >= PersistentErrorThreshold;
    }

    /// <summary>
    /// Determines if this is strategic element progression (student trying different elements).
    /// Should exclude from tinkering detection.
    /// </summary>
    private static bool ShouldExcludeAsStrategicProgression(
        CodeSubmission current,
        CodeSubmission? previous,
        List<CodeSubmission> history)
    {
        if (previous == null) return false;

        // Exclusion: If error type changed, student is exploring different approaches
        if (current.DiagnosticCategory != previous.DiagnosticCategory)
            return true;

        // Exclusion: If significant structural change (ED >= 10), likely rework not cycling
        if (current.EditDistance >= StrategicChangeThreshold)
            return true;

        return false;
    }

    /// <summary>
    /// Determines if this is a reflection pause followed by recovery.
    /// Should exclude from tinkering penalty if student recovers after pausing.
    /// </summary>
    private static bool ShouldExcludeAsReflectionRecovery(
        CodeSubmission current,
        CodeSubmission? previous,
        List<CodeSubmission> history)
    {
        if (previous == null) return false;

        // Exclusion: Large pause (> 15s) followed by significant change + different error = recovery
        if (current.SubmissionIntervalSeconds > ReflectionPauseMin)
        {
            bool majorChange = current.EditDistance >= StrategicChangeThreshold;
            bool differentError = current.DiagnosticCategory != previous.DiagnosticCategory;
            bool hasRecovery = history.Any(s => s.IsCorrect);
            
            if (majorChange && differentError && hasRecovery)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Evaluates confidence level and returns appropriate weight for Tinkering.
    /// 
    /// HIGH: Multiple indicators OR explicit SI<6s OR character/value cycling
    /// MODERATE: Persistent error (Indicator #4) alone without rapid SI
    /// LOW: Weak evidence requiring monitoring
    /// </summary>
    private static (ConfidenceLevel, double) EvaluateTinkeringConfidence(
        bool rapidSI,
        bool charCycling,
        bool numericCycling,
        bool persistentError,
        bool noStructuralChange)
    {
        int indicatorCount = (rapidSI ? 1 : 0) 
                           + (charCycling ? 1 : 0) 
                           + (numericCycling ? 1 : 0) 
                           + (persistentError ? 1 : 0);

        // HIGH: Multiple indicators OR explicit cycling evidence
        if (indicatorCount >= 2 || charCycling || numericCycling || (rapidSI && persistentError))
            return (ConfidenceLevel.High, WeightTinkeringHigh);

        // MODERATE: Single strong indicator (persistent error or rapid SI alone)
        if (indicatorCount == 1 && (persistentError || rapidSI))
            return (ConfidenceLevel.Moderate, WeightTinkeringModerate);

        // LOW: Weak evidence
        return (ConfidenceLevel.Low, WeightTinkeringLow);
    }

    /// <summary>Helper to generate human-readable summary of which indicators triggered.</summary>
    private static string GetTinkeringIndicatorSummary(
        bool ind1, bool ind2, bool ind3, bool ind4, bool ind5)
    {
        var parts = new List<string>();
        if (ind1) parts.Add("SI<6s");
        if (ind2) parts.Add("CharCycling(3x)");
        if (ind3) parts.Add("NumericCycling");
        if (ind4) parts.Add("PersistentError");
        if (ind5) parts.Add("NoStructChange");
        return string.Join(" + ", parts);
    }

    /// <summary>Helper to extract numeric literals from source code.</summary>
    private static List<int> ExtractNumericLiterals(string code)
    {
        var matches = Regex.Matches(code, @"\b(\d+)\b");
        return matches.Cast<Match>().Select(m => int.Parse(m.Groups[1].Value)).ToList();
    }

    /// HintWithheld: SI > 15s AND a new/different error (student is actively exploring)
    private static bool IsHintWithheld(CodeSubmission c, CodeSubmission? prev)
    {
        if (c.SubmissionIntervalSeconds <= ThinkingIntervalMin) return false;
        if (c.IsCorrect) return false;
        if (prev == null) return true; // First attempt after a long pause = benefit of the doubt
        return c.DiagnosticCategory != prev.DiagnosticCategory;
    }

    /// <summary>
    /// Comprehensive ActiveThinking detection.
    /// Evaluates multiple indicators:
    ///   1. Pre-task pause >= 10s OR SI > 15s
    ///   2. Typing burst coverage >= 60%
    ///   3. Single or no system checks (<= 1)
    ///   4. Self-corrections present (> 0)
    ///   5. >= 2 progressive submissions
    /// </summary>
    private HbdaResult? EvaluateActiveThinking(
        CodeSubmission current,
        CodeSubmission? previous,
        List<CodeSubmission> sessionHistory)
    {
        // Must exclude: full external paste
        if (current.PasteDetected) return null;

        bool isExtendedPause = current.SubmissionIntervalSeconds > ThinkingIntervalMin || 
                               (current.InitialLatencyMs / 1000.0) >= PreTaskPauseMin;
        
        bool hasHighCoverage = current.TypingBurstCoverage >= TypingBurstCoverageMin;
        bool hasMinimalSystemChecks = current.SystemCheckCount <= MaxSystemChecks;
        bool hasSelfCorrections = current.SelfCorrectionCount > 0;
        
        bool isCurrentProgressive = current.IsCorrect || 
            (previous != null && current.DiagnosticCategory != previous.DiagnosticCategory);

        // Previous submission must also have been progressive for indicator
        bool isPrevProgressive = false;
        if (previous != null && sessionHistory.Count >= 2)
        {
            var beforePrev = sessionHistory.OrderByDescending(h => h.SubmittedAt).Skip(1).FirstOrDefault();
            isPrevProgressive = previous.IsCorrect || 
                (beforePrev != null && previous.DiagnosticCategory != beforePrev.DiagnosticCategory);
        }

        bool hasTwoProgressive = isCurrentProgressive && isPrevProgressive;

        // At minimum, we need an extended pause OR high typing burst coverage
        if (!isExtendedPause && !hasHighCoverage) return null;

        // Must be at least progressive
        if (!isCurrentProgressive) return null;

        // Exclusion: Single long pause followed by luck or a correct guess without additional progressive submits.
        // A "correct guess" means low typing burst coverage (not a full typing burst).
        if (isExtendedPause && !hasTwoProgressive && current.TypingBurstCoverage < TypingBurstCoverageMin)
        {
            return null; // Exclude
        }

        // Evaluate Confidence
        var (confidenceLevel, delta) = EvaluateActiveThinkingConfidence(
            isExtendedPause, hasHighCoverage, hasTwoProgressive, hasMinimalSystemChecks, hasSelfCorrections, current.IsCorrect);

        var indicatorSummary = GetActiveThinkingIndicatorSummary(isExtendedPause, hasHighCoverage, hasTwoProgressive, hasMinimalSystemChecks, hasSelfCorrections);

        return Result(
            BehaviorState.ActiveThinking,
            delta,
            confidenceLevel,
            $"ActiveThinking ({confidenceLevel}): {indicatorSummary}");
    }

    private static (ConfidenceLevel, double) EvaluateActiveThinkingConfidence(
        bool isExtendedPause, bool hasHighCoverage, bool hasTwoProgressive, 
        bool hasMinimalSystemChecks, bool hasSelfCorrections, bool isCorrect)
    {
        // HIGH if Extended pause, at least 2 progressive submits, single typing burst, and correct or near-correct outcome.
        if (isExtendedPause && hasTwoProgressive && hasHighCoverage)
            return (ConfidenceLevel.High, WeightActiveThinkingHigh);

        // MODERATE if Pauses present but session is fragmented or outcome is incorrect.
        if (isExtendedPause && (!hasHighCoverage || !isCorrect))
            return (ConfidenceLevel.Moderate, WeightActiveThinkingModerate);

        // LOW if Indicators partially present with ambiguous outcome.
        return (ConfidenceLevel.Low, WeightActiveThinkingLow);
    }

    private static string GetActiveThinkingIndicatorSummary(
        bool pause, bool coverage, bool prog, bool sysChecks, bool selfCorr)
    {
        var parts = new List<string>();
        if (pause) parts.Add("ExtendedPause");
        if (coverage) parts.Add("Burst>=60%");
        if (prog) parts.Add(">=2Progressive");
        if (sysChecks) parts.Add("MinimalSysChecks");
        if (selfCorr) parts.Add("SelfCorrections");
        return string.Join(" + ", parts);
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

    private static HbdaResult Result(BehaviorState state, double delta, ConfidenceLevel confidence, string reason) =>
        new() 
        { 
            State = state, 
            HelplessnessScoreDelta = delta, 
            Reasoning = reason,
            Confidence = confidence
        };
}
