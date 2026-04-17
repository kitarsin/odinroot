namespace ODIN.Api.Services;

/// <summary>
/// Levenshtein Edit Distance calculator used by the HBDA to measure
/// how much the student changed between submissions.
/// ED &lt;= 2 = Tinkering, ED ~= 0 = Gaming, ED &gt;= 10 = Productive Failure.
/// </summary>
public static class EditDistanceCalculator
{
    /// <summary>
    /// Computes the Levenshtein distance between two strings.
    /// Returns the minimum number of single-character edits (insertions,
    /// deletions, or substitutions) needed to transform one string into another.
    /// </summary>
    public static int Compute(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return string.IsNullOrEmpty(target) ? 0 : target.Length;
        if (string.IsNullOrEmpty(target))
            return source.Length;

        int sourceLen = source.Length;
        int targetLen = target.Length;

        // Use two-row optimization for memory efficiency
        var previousRow = new int[targetLen + 1];
        var currentRow = new int[targetLen + 1];

        for (int j = 0; j <= targetLen; j++)
            previousRow[j] = j;

        for (int i = 1; i <= sourceLen; i++)
        {
            currentRow[0] = i;

            for (int j = 1; j <= targetLen; j++)
            {
                int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                currentRow[j] = Math.Min(
                    Math.Min(currentRow[j - 1] + 1,      // insertion
                             previousRow[j] + 1),         // deletion
                    previousRow[j - 1] + cost);            // substitution
            }

            // Swap rows
            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[targetLen];
    }
}
