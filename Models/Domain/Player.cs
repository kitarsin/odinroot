namespace ODIN.Api.Models.Domain;

/// <summary>
/// Maps to the existing `profiles` table in Supabase.
///
/// Columns: id, student_id, full_name, section, role, sync_rate, avatar_url, created_at, achievements,
/// current_level, experience_points, helplessness_score, total_submissions, updated_at, pretest_completed, game_state.
/// </summary>
public class Player
{
    // ── Original `profiles` columns ──
    public Guid Id { get; set; }
    public string? StudentId { get; set; }
    public string? DisplayName { get; set; }    // maps to full_name
    public string? Section { get; set; }
    public string Role { get; set; } = "student";
    public int SyncRate { get; set; } = 100;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Achievements { get; set; } = "[]";  // jsonb
    public bool PretestCompleted { get; set; } = false;

    // ── ODIN-specific columns (added by migration) ──
    public int CurrentLevel { get; set; } = 0;
    public int ExperiencePoints { get; set; } = 0;
    public double HelplessnessScore { get; set; } = 0;
    public int TotalSubmissions { get; set; } = 0;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string GameState { get; set; } = "{}"; // JSONB: Godot player_data (character, level, enemies, dialogues)

    // Navigation properties
    public ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
    public ICollection<MasteryState> MasteryStates { get; set; } = new List<MasteryState>();
}

/// <summary>
/// Maps to the existing `progress` table in Supabase.
/// Composite PK: (user_id, topic).
///
/// Original: user_id, topic, mastery_percentage, is_locked
/// Added:    probability_mastery, attempt_count, consecutive_correct, is_mastered, updated_at
///
/// The trigger `trg_sync_mastery` auto-updates mastery_percentage
/// whenever probability_mastery changes, keeping them in sync.
/// </summary>
public class MasteryState
{
    // ── Original `progress` columns ──
    public Guid UserId { get; set; }            // PK part 1
    public string Topic { get; set; } = "";     // PK part 2 (e.g. "ArrayIteration")
    public int MasteryPercentage { get; set; } = 0;  // Auto-synced via trigger
    public bool IsLocked { get; set; } = true;

    // ── ODIN-specific columns ──
    public double ProbabilityMastery { get; set; } = 0.10;  // BKT P(L)
    public int AttemptCount { get; set; } = 0;
    public int ConsecutiveCorrect { get; set; } = 0;
    public bool IsMastered { get; set; } = false;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Player Player { get; set; } = null!;
}
