namespace ODIN.Api.Models.Enums;

/// <summary>
/// Specific diagnostic categories detected by the AST-based Code Diagnosis Engine.
/// These are the structural misconceptions the Roslyn parser checks for.
/// </summary>
public enum DiagnosticCategory
{
    /// <summary>No errors detected — code compiles and passes logic checks.</summary>
    None,

    /// <summary>Roslyn compiler syntax error (e.g., missing semicolons, braces).</summary>
    SyntaxError,

    /// <summary>Off-by-One error: loop uses i &lt;= arr.Length instead of i &lt; arr.Length.</summary>
    OffByOneError,

    /// <summary>Array accessed with a hardcoded out-of-bounds index.</summary>
    IndexOutOfRange,

    /// <summary>Array declared but never initialized (e.g., int[] arr; then used).</summary>
    UninitializedArray,

    /// <summary>Array declared with invalid size (zero or negative).</summary>
    InvalidArraySize,

    /// <summary>Wrong number of dimensions used when accessing multidimensional arrays.</summary>
    DimensionMismatch,

    /// <summary>Loop iterating over array but never accessing elements via index.</summary>
    UnusedLoopIndex,

    /// <summary>Infinite loop detected — loop condition can never become false.</summary>
    InfiniteLoop,

    /// <summary>Generic logic error not covered by specific rules.</summary>
    GenericLogicError,

    /// <summary>Submitted code is identical to the provided starter template — no solution attempt detected.</summary>
    UnchangedStarterCode,

    /// <summary>Client reported the battle/session ended without a final graded compile (telemetry only).</summary>
    SessionAbandoned
}
