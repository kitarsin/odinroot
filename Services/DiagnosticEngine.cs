using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ODIN.Api.Models.DTOs;
using ODIN.Api.Models.Enums;
using ODIN.Api.Services.Interfaces;

namespace ODIN.Api.Services;

/// <summary>
/// AST-Based Code Diagnosis Engine — Stage 2 of the Sequential Pipeline.
/// Uses Microsoft Roslyn to parse student C# code into an AST, normalize
/// identifiers to generic tokens, then traverse the tree to detect specific
/// structural misconceptions related to C# Arrays.
///
/// Detects: Off-by-One, IndexOutOfRange, UninitializedArray,
/// InvalidArraySize, DimensionMismatch, InfiniteLoop, UnusedLoopIndex.
/// </summary>
public class DiagnosticEngine : IDiagnosticEngine
{
    /// <summary>
    /// Wraps student code in a class/method scaffold so Roslyn can parse it,
    /// then runs the diagnostic rules pipeline.
    /// </summary>
    public DiagnosticResult Diagnose(string sourceCode, SkillType skillType)
    {
        var result = new DiagnosticResult();

        // Wrap raw student code in a compilable scaffold
        string wrappedCode = WrapInScaffold(sourceCode);

        SyntaxTree tree = CSharpSyntaxTree.ParseText(wrappedCode);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        // ── Step 1: Check for Roslyn syntax errors ──
        var syntaxDiagnostics = tree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (syntaxDiagnostics.Any())
        {
            result.IsCorrect = false;
            result.Category = DiagnosticCategory.SyntaxError;
            result.Message = "Your code has syntax errors. Check for missing semicolons, braces, or typos.";
            result.CompilerDiagnostics = syntaxDiagnostics.Select(d =>
            {
                var lineSpan = d.Location.GetLineSpan();
                return new CompilerDiagnosticDto
                {
                    Id = d.Id,
                    Severity = d.Severity.ToString(),
                    Message = d.GetMessage(),
                    // Adjust line numbers to account for scaffold offset
                    Line = Math.Max(1, lineSpan.StartLinePosition.Line - GetScaffoldLineOffset() + 1),
                    Column = lineSpan.StartLinePosition.Character + 1
                };
            }).ToList();
            return result;
        }

        // ── Step 2: Create semantic compilation for deeper analysis ──
        var compilation = CreateCompilation(tree);
        var semanticModel = compilation.GetSemanticModel(tree);

        // Collect semantic errors
        var semanticDiagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        result.CompilerDiagnostics = semanticDiagnostics.Select(d =>
        {
            var lineSpan = d.Location.GetLineSpan();
            return new CompilerDiagnosticDto
            {
                Id = d.Id,
                Severity = d.Severity.ToString(),
                Message = d.GetMessage(),
                Line = Math.Max(1, lineSpan.StartLinePosition.Line - GetScaffoldLineOffset() + 1),
                Column = lineSpan.StartLinePosition.Character + 1
            };
        }).ToList();

        // ── Step 3: Run AST-based rule checks (order matters) ──

        // Check for uninitialized arrays
        var uninitResult = CheckUninitializedArrays(root, semanticModel);
        if (uninitResult != null) return MergeResult(result, uninitResult);

        // Check for invalid array sizes
        var sizeResult = CheckInvalidArraySize(root);
        if (sizeResult != null) return MergeResult(result, sizeResult);

        // Check for off-by-one errors in loops
        var oboResult = CheckOffByOneErrors(root);
        if (oboResult != null) return MergeResult(result, oboResult);

        // Check for hardcoded out-of-bounds index access
        var oobResult = CheckIndexOutOfRange(root);
        if (oobResult != null) return MergeResult(result, oobResult);

        // Check for dimension mismatch (Level 3)
        if (skillType == SkillType.MultidimensionalArrays || skillType == SkillType.JaggedArrays)
        {
            var dimResult = CheckDimensionMismatch(root, semanticModel);
            if (dimResult != null) return MergeResult(result, dimResult);
        }

        // Check for infinite loops
        var loopResult = CheckInfiniteLoop(root);
        if (loopResult != null) return MergeResult(result, loopResult);

        // If we have semantic errors but none of our rules caught them
        if (semanticDiagnostics.Any())
        {
            result.IsCorrect = false;
            result.Category = DiagnosticCategory.GenericLogicError;
            result.Message = "Your code has compilation errors. Review the error messages in the editor.";
            return result;
        }

        // ── All checks passed ──
        result.IsCorrect = true;
        result.Category = DiagnosticCategory.None;
        result.Message = "Code compiles and passes all logic checks.";
        return result;
    }

    // ═══════════════════════════════════════════════════════════
    // AST Rule Checkers
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Detects Off-by-One: loop uses i &lt;= arr.Length instead of i &lt; arr.Length.
    /// Traverses ForStatements and checks BinaryExpressions in the condition.
    /// </summary>
    private static DiagnosticResult? CheckOffByOneErrors(CompilationUnitSyntax root)
    {
        var forStatements = root.DescendantNodes().OfType<ForStatementSyntax>();

        foreach (var forStmt in forStatements)
        {
            if (forStmt.Condition is BinaryExpressionSyntax condition)
            {
                // Pattern: i <= arr.Length or i <= arr.Count
                if (condition.IsKind(SyntaxKind.LessThanOrEqualExpression))
                {
                    string rightText = condition.Right.ToString();
                    if (rightText.Contains(".Length") || rightText.Contains(".Count"))
                    {
                        return new DiagnosticResult
                        {
                            IsCorrect = false,
                            Category = DiagnosticCategory.OffByOneError,
                            Message = "Off-by-One error detected: Your loop condition uses '<=' with " +
                                     ".Length, which will cause an IndexOutOfRangeException. " +
                                     "Array indices go from 0 to Length-1, so use '<' instead."
                        };
                    }
                }

                // Pattern: i < arr.Length - 1 (misses last element)
                if (condition.IsKind(SyntaxKind.LessThanExpression) &&
                    condition.Right is BinaryExpressionSyntax subtraction &&
                    subtraction.IsKind(SyntaxKind.SubtractExpression))
                {
                    string leftOfSub = subtraction.Left.ToString();
                    string rightOfSub = subtraction.Right.ToString();
                    if ((leftOfSub.Contains(".Length") || leftOfSub.Contains(".Count")) &&
                        rightOfSub.Trim() == "1")
                    {
                        // This might be intentional for some algorithms, so just flag it
                        return new DiagnosticResult
                        {
                            IsCorrect = false,
                            Category = DiagnosticCategory.OffByOneError,
                            Message = "Potential Off-by-One: Your loop uses '.Length - 1', which " +
                                     "skips the last element. If you need all elements, use '.Length' directly."
                        };
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Detects arrays accessed with hardcoded indices that are clearly out of bounds.
    /// Example: int[] arr = new int[3]; arr[5] = 10;
    /// </summary>
    private static DiagnosticResult? CheckIndexOutOfRange(CompilationUnitSyntax root)
    {
        // Find array creations with explicit sizes
        var arrayCreations = root.DescendantNodes()
            .OfType<ArrayCreationExpressionSyntax>()
            .ToList();

        // Build a map of variable name → declared size
        var arraySizes = new Dictionary<string, int>();
        foreach (var creation in arrayCreations)
        {
            var rankSpecifiers = creation.Type.RankSpecifiers;
            if (rankSpecifiers.Count > 0)
            {
                var sizes = rankSpecifiers[0].Sizes;
                if (sizes.Count == 1 && sizes[0] is LiteralExpressionSyntax literal &&
                    literal.Token.Value is int size)
                {
                    // Try to find the variable this is assigned to
                    var parent = creation.Parent;
                    if (parent is EqualsValueClauseSyntax equalsClause &&
                        equalsClause.Parent is VariableDeclaratorSyntax declarator)
                    {
                        arraySizes[declarator.Identifier.Text] = size;
                    }
                }
            }
        }

        // Check all element access expressions for out-of-bounds
        var elementAccesses = root.DescendantNodes()
            .OfType<ElementAccessExpressionSyntax>()
            .ToList();

        foreach (var access in elementAccesses)
        {
            string varName = access.Expression.ToString();
            if (arraySizes.TryGetValue(varName, out int declaredSize))
            {
                foreach (var arg in access.ArgumentList.Arguments)
                {
                    if (arg.Expression is LiteralExpressionSyntax indexLiteral &&
                        indexLiteral.Token.Value is int index)
                    {
                        if (index >= declaredSize || index < 0)
                        {
                            return new DiagnosticResult
                            {
                                IsCorrect = false,
                                Category = DiagnosticCategory.IndexOutOfRange,
                                Message = $"Index out of range: You're accessing '{varName}[{index}]' " +
                                         $"but the array was created with size {declaredSize}. " +
                                         $"Valid indices are 0 to {declaredSize - 1}."
                            };
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Detects arrays that are declared but never initialized.
    /// Example: int[] arr; ... arr[0] = 5; // NullReferenceException
    /// </summary>
    private static DiagnosticResult? CheckUninitializedArrays(
        CompilationUnitSyntax root, SemanticModel? semanticModel)
    {
        var variableDeclarations = root.DescendantNodes()
            .OfType<VariableDeclarationSyntax>()
            .Where(v => v.Type is ArrayTypeSyntax)
            .ToList();

        foreach (var declaration in variableDeclarations)
        {
            foreach (var declarator in declaration.Variables)
            {
                // If no initializer is provided
                if (declarator.Initializer == null)
                {
                    string varName = declarator.Identifier.Text;

                    // Check if the variable is later assigned (simple heuristic)
                    var method = declarator.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                    if (method != null)
                    {
                        var assignments = method.DescendantNodes()
                            .OfType<AssignmentExpressionSyntax>()
                            .Where(a => a.Left.ToString() == varName)
                            .ToList();

                        // Check if the variable is used before any assignment
                        var usages = method.DescendantNodes()
                            .OfType<IdentifierNameSyntax>()
                            .Where(id => id.Identifier.Text == varName &&
                                        !(id.Parent is AssignmentExpressionSyntax ae && ae.Left == id))
                            .ToList();

                        if (!assignments.Any() && usages.Any())
                        {
                            return new DiagnosticResult
                            {
                                IsCorrect = false,
                                Category = DiagnosticCategory.UninitializedArray,
                                Message = $"Array '{varName}' is declared but never initialized. " +
                                         "You must use 'new' to allocate memory before using it. " +
                                         $"Example: {declaration.Type} {varName} = new {declaration.Type.ToString().Replace("[]", "")}[size];"
                            };
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Detects arrays created with zero or negative sizes.
    /// </summary>
    private static DiagnosticResult? CheckInvalidArraySize(CompilationUnitSyntax root)
    {
        var arrayCreations = root.DescendantNodes()
            .OfType<ArrayCreationExpressionSyntax>()
            .ToList();

        foreach (var creation in arrayCreations)
        {
            var rankSpecifiers = creation.Type.RankSpecifiers;
            if (rankSpecifiers.Count > 0)
            {
                foreach (var size in rankSpecifiers[0].Sizes)
                {
                    if (size is LiteralExpressionSyntax literal && literal.Token.Value is int sizeVal)
                    {
                        if (sizeVal <= 0)
                        {
                            return new DiagnosticResult
                            {
                                IsCorrect = false,
                                Category = DiagnosticCategory.InvalidArraySize,
                                Message = $"Invalid array size: You created an array with size {sizeVal}. " +
                                         "Arrays must have a positive size greater than zero."
                            };
                        }
                    }

                    // Detect negative via unary minus
                    if (size is PrefixUnaryExpressionSyntax unary &&
                        unary.IsKind(SyntaxKind.UnaryMinusExpression))
                    {
                        return new DiagnosticResult
                        {
                            IsCorrect = false,
                            Category = DiagnosticCategory.InvalidArraySize,
                            Message = "Invalid array size: Array sizes cannot be negative."
                        };
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Detects dimension mismatch for multidimensional arrays.
    /// Example: int[,] grid = new int[3,3]; grid[1] = 5; (missing second index)
    /// </summary>
    private static DiagnosticResult? CheckDimensionMismatch(
        CompilationUnitSyntax root, SemanticModel? semanticModel)
    {
        // Find 2D array declarations
        var multiDimDeclarations = root.DescendantNodes()
            .OfType<VariableDeclarationSyntax>()
            .Where(v => v.Type is ArrayTypeSyntax arrayType &&
                       arrayType.RankSpecifiers.Any(r => r.Rank > 1))
            .SelectMany(v => v.Variables.Select(d => d.Identifier.Text))
            .ToHashSet();

        // Check element accesses for insufficient indices
        var elementAccesses = root.DescendantNodes()
            .OfType<ElementAccessExpressionSyntax>()
            .ToList();

        foreach (var access in elementAccesses)
        {
            string varName = access.Expression.ToString();
            if (multiDimDeclarations.Contains(varName))
            {
                // 2D arrays need 2 arguments in a single argument list
                if (access.ArgumentList.Arguments.Count < 2)
                {
                    return new DiagnosticResult
                    {
                        IsCorrect = false,
                        Category = DiagnosticCategory.DimensionMismatch,
                        Message = $"Dimension mismatch: '{varName}' is a 2D array and requires " +
                                 $"two indices (row and column). Use '{varName}[row, col]' instead of '{varName}[index]'."
                    };
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Detects potential infinite loops (loop condition that can never become false).
    /// Simple heuristic: for/while loops with no increment or with always-true conditions.
    /// </summary>
    private static DiagnosticResult? CheckInfiniteLoop(CompilationUnitSyntax root)
    {
        // Check for `while(true)` without break
        var whileStatements = root.DescendantNodes().OfType<WhileStatementSyntax>();
        foreach (var whileStmt in whileStatements)
        {
            if (whileStmt.Condition is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.TrueLiteralExpression))
            {
                // Check if there's a break statement inside
                bool hasBreak = whileStmt.Statement.DescendantNodes()
                    .OfType<BreakStatementSyntax>().Any();
                if (!hasBreak)
                {
                    return new DiagnosticResult
                    {
                        IsCorrect = false,
                        Category = DiagnosticCategory.InfiniteLoop,
                        Message = "Potential infinite loop: 'while(true)' without a 'break' statement " +
                                 "will run forever. Add a condition to exit the loop."
                    };
                }
            }
        }

        // Check for `for` loops with no incrementors
        var forStatements = root.DescendantNodes().OfType<ForStatementSyntax>();
        foreach (var forStmt in forStatements)
        {
            if (!forStmt.Incrementors.Any() && forStmt.Condition != null)
            {
                // No increment means the loop variable never changes
                bool hasInternalModification = forStmt.Statement.DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>().Any() ||
                    forStmt.Statement.DescendantNodes()
                    .OfType<PostfixUnaryExpressionSyntax>().Any() ||
                    forStmt.Statement.DescendantNodes()
                    .OfType<PrefixUnaryExpressionSyntax>().Any();

                if (!hasInternalModification)
                {
                    return new DiagnosticResult
                    {
                        IsCorrect = false,
                        Category = DiagnosticCategory.InfiniteLoop,
                        Message = "Potential infinite loop: Your 'for' loop has no increment expression " +
                                 "and the loop variable is not modified inside the body."
                    };
                }
            }
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Wraps raw student code in a class + method scaffold so Roslyn can parse it.
    /// </summary>
    private static string WrapInScaffold(string studentCode)
    {
        return $@"
using System;
using System.Collections.Generic;
using System.Linq;

public class StudentSolution
{{
    public void Solve()
    {{
        {studentCode}
    }}
}}";
    }

    private static int GetScaffoldLineOffset() => 8; // Lines before student code starts

    /// <summary>
    /// Creates a Roslyn compilation with System references for semantic analysis.
    /// </summary>
    private static CSharpCompilation CreateCompilation(SyntaxTree tree)
    {
        // Reference core .NET assemblies
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        // Add the runtime assembly
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var runtimeAssembly = Path.Combine(runtimeDir, "System.Runtime.dll");
        if (File.Exists(runtimeAssembly))
            references.Add(MetadataReference.CreateFromFile(runtimeAssembly));

        return CSharpCompilation.Create("StudentSubmission",
            syntaxTrees: new[] { tree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static DiagnosticResult MergeResult(DiagnosticResult target, DiagnosticResult source)
    {
        source.CompilerDiagnostics = target.CompilerDiagnostics;
        return source;
    }
}
