using System.Linq;
using ODIN.Api.Models.Domain;

namespace ODIN.Api.Services;

/// <summary>
/// Derives HBDA-related fields from raw keystroke batches when the client omits them.
/// </summary>
public static class SubmissionTelemetryHelper
{
    private const double BurstGapMs = 450.0;

    /// <summary>
    /// Longest run of key-down events with inter-event gaps ≤ BurstGapMs, divided by task text size.
    /// </summary>
    public static double EstimateTypingBurstCoverage(
        IReadOnlyList<double[]>? rawEvents,
        string sourceCode,
        string starterReference)
    {
        if (rawEvents is null || rawEvents.Count == 0) return 0.0;

        var maxBurstKeyDowns = 0;
        var currentBurst = 0;
        double lastT = -1;
        foreach (var ev in rawEvents)
        {
            if (ev.Length < 3) continue;
            var t = ev[0];
            var phase = (int)ev[2];
            if (lastT >= 0 && t - lastT > BurstGapMs)
            {
                maxBurstKeyDowns = Math.Max(maxBurstKeyDowns, currentBurst);
                currentBurst = 0;
            }

            if (phase == 0)
                currentBurst++;
            lastT = t;
        }

        maxBurstKeyDowns = Math.Max(maxBurstKeyDowns, currentBurst);

        var denom = Math.Max(Math.Max(sourceCode?.Length ?? 0, starterReference?.Length ?? 0), 1);
        return Math.Clamp(maxBurstKeyDowns / (double)denom, 0, 1);
    }

    /// <summary>Counts key-down events for rapid-dismiss / zero-interaction checks.</summary>
    public static int CountKeyDowns(IReadOnlyList<double[]>? rawEvents)
    {
        if (rawEvents is null) return 0;
        var n = 0;
        foreach (var ev in rawEvents)
        {
            if (ev.Length >= 3 && (int)ev[2] == 0)
                n++;
        }

        return n;
    }

    /// <summary>
    /// Godot physical keycodes for Backspace / Delete (GL Compatibility builds).
    /// </summary>
    public static int EstimateSelfCorrectionsFromRaw(IReadOnlyList<double[]>? rawEvents)
    {
        if (rawEvents is null) return 0;
        const int godotBackspace = 4194305;
        const int godotDelete = 4194312;
        var n = 0;
        foreach (var ev in rawEvents)
        {
            if (ev.Length < 3 || (int)ev[2] != 0) continue;
            var k = (int)ev[1];
            if (k == godotBackspace || k == godotDelete)
                n++;
        }

        return n;
    }

    /// <summary>
    /// How many trailing compiles used the same normalized source as the current request (identical “system checks”).
    /// </summary>
    public static int CountTrailingIdenticalCompileChecks(
        List<CodeSubmission> sessionHistoryChronological,
        string currentSource,
        Func<string, string> normalize)
    {
        var cur = normalize(currentSource);
        var n = 0;
        foreach (var s in sessionHistoryChronological.OrderByDescending(h => h.SubmittedAt))
        {
            if (normalize(s.SourceCode) == cur)
                n++;
            else
                break;
        }

        return n;
    }
}
