namespace ODIN.Api.Models.Enums;

/// <summary>
/// C# Array skill categories mapped to the three dungeon levels.
/// Level 1: SingleDimensional (initialization &amp; access)
/// Level 2: Iteration (looping through arrays)
/// Level 3: Multidimensional (matrix &amp; jagged arrays)
/// </summary>
public enum SkillType
{
    /// <summary>Level 1 — Array declaration, initialization, and element access.</summary>
    ArrayInitialization,

    /// <summary>Level 1 — Accessing array elements by index.</summary>
    ArrayAccess,

    /// <summary>Level 2 — Iterating over arrays using loops (for, foreach, while).</summary>
    ArrayIteration,

    /// <summary>Level 2 — Common loop-based array operations (sum, search, max/min).</summary>
    ArrayOperations,

    /// <summary>Level 3 — Two-dimensional (matrix) array declaration and access.</summary>
    MultidimensionalArrays,

    /// <summary>Level 3 — Jagged array declaration and nested access.</summary>
    JaggedArrays
}
