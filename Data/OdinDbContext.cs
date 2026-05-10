using Microsoft.EntityFrameworkCore;
using ODIN.Api.Models.Domain;

namespace ODIN.Api.Data;

public class OdinDbContext : DbContext
{
    public OdinDbContext(DbContextOptions<OdinDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<MasteryState> MasteryStates => Set<MasteryState>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<CodeSubmission> CodeSubmissions => Set<CodeSubmission>();
    public DbSet<InteractionLog> InteractionLogs => Set<InteractionLog>();
    public DbSet<ScaffoldingHint> ScaffoldingHints => Set<ScaffoldingHint>();
    public DbSet<Puzzle> Puzzles => Set<Puzzle>();
    public DbSet<KeystrokeRawEventBatch> KeystrokeRawEventBatches => Set<KeystrokeRawEventBatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");

        // ── profiles → Player ──
        modelBuilder.Entity<Player>(e =>
        {
            e.ToTable("profiles");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.StudentId).HasColumnName("student_id");
            e.Property(p => p.DisplayName).HasColumnName("full_name");
            e.Property(p => p.Section).HasColumnName("section");
            e.Property(p => p.Role).HasColumnName("role");
            e.Property(p => p.SyncRate).HasColumnName("sync_rate");
            e.Property(p => p.AvatarUrl).HasColumnName("avatar_url");
            e.Property(p => p.CreatedAt).HasColumnName("created_at");
            e.Property(p => p.Achievements).HasColumnName("achievements").HasColumnType("jsonb");
            e.Property(p => p.CurrentLevel).HasColumnName("current_level");
            e.Property(p => p.ExperiencePoints).HasColumnName("experience_points");
            e.Property(p => p.HelplessnessScore).HasColumnName("helplessness_score");
            e.Property(p => p.TotalSubmissions).HasColumnName("total_submissions");
            e.Property(p => p.UpdatedAt).HasColumnName("updated_at");
            e.Property(p => p.GameState).HasColumnName("game_state").HasColumnType("jsonb");
            e.HasIndex(p => p.StudentId).IsUnique();
        });

        // ── progress → MasteryState ──
        modelBuilder.Entity<MasteryState>(e =>
        {
            e.ToTable("progress");
            e.HasKey(m => new { m.UserId, m.Topic });
            e.Property(m => m.UserId).HasColumnName("user_id");
            e.Property(m => m.Topic).HasColumnName("topic");
            e.Property(m => m.MasteryPercentage).HasColumnName("mastery_percentage");
            e.Property(m => m.IsLocked).HasColumnName("is_locked");
            e.Property(m => m.ProbabilityMastery).HasColumnName("probability_mastery");
            e.Property(m => m.AttemptCount).HasColumnName("attempt_count");
            e.Property(m => m.ConsecutiveCorrect).HasColumnName("consecutive_correct");
            e.Property(m => m.IsMastered).HasColumnName("is_mastered");
            e.Property(m => m.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(m => m.Player).WithMany(p => p.MasteryStates).HasForeignKey(m => m.UserId);
        });

        // ── submissions → CodeSubmission ──
        modelBuilder.Entity<CodeSubmission>(e =>
        {
            e.ToTable("submissions");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id");
            e.Property(s => s.UserId).HasColumnName("user_id");
            e.Property(s => s.QuestionId).HasColumnName("question_id");
            e.Property(s => s.SourceCode).HasColumnName("code_snippet");
            e.Property(s => s.IsCorrect).HasColumnName("is_correct");
            e.Property(s => s.ErrorMessage).HasColumnName("error_message");
            e.Property(s => s.ExecutionTimeMs).HasColumnName("execution_time_ms");
            e.Property(s => s.SubmittedAt).HasColumnName("created_at");
            e.Property(s => s.SessionId).HasColumnName("session_id");
            e.Property(s => s.SkillType).HasColumnName("skill_type");
            e.Property(s => s.AverageFlightTimeMs).HasColumnName("avg_flight_time_ms");
            e.Property(s => s.AverageDwellTimeMs).HasColumnName("avg_dwell_time_ms");
            e.Property(s => s.InitialLatencyMs).HasColumnName("initial_latency_ms");
            e.Property(s => s.TotalTimeSeconds).HasColumnName("total_time_seconds");
            e.Property(s => s.EditDistance).HasColumnName("edit_distance");
            e.Property(s => s.SubmissionIntervalSeconds).HasColumnName("submission_interval_seconds");
            e.Property(s => s.HintUsageCount).HasColumnName("hint_usage_count");
            e.Property(s => s.DiagnosticCategory).HasColumnName("diagnostic_category");
            e.Property(s => s.DiagnosticMessage).HasColumnName("diagnostic_message");
            e.Property(s => s.BehaviorState).HasColumnName("behavior_state");
            e.Property(s => s.InterventionType).HasColumnName("intervention_type");
            e.HasIndex(s => s.UserId);
            e.HasIndex(s => s.SessionId);
            e.HasOne<GameSession>().WithMany(g => g.Submissions).HasForeignKey(s => s.SessionId);
        });

        // ── game_sessions → GameSession ──
        modelBuilder.Entity<GameSession>(e =>
        {
            e.ToTable("game_sessions");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id");
            e.Property(s => s.UserId).HasColumnName("user_id");
            e.Property(s => s.DungeonLevel).HasColumnName("dungeon_level");
            e.Property(s => s.PuzzleId).HasColumnName("puzzle_id");
            e.Property(s => s.StartedAt).HasColumnName("started_at");
            e.Property(s => s.EndedAt).HasColumnName("ended_at");
            e.Property(s => s.SubmissionCount).HasColumnName("submission_count");
            e.Property(s => s.IsCompleted).HasColumnName("is_completed");
            e.HasOne(s => s.Player).WithMany(p => p.GameSessions).HasForeignKey(s => s.UserId);
        });

        // ── interaction_logs → InteractionLog ──
        modelBuilder.Entity<InteractionLog>(e =>
        {
            e.ToTable("interaction_logs");
            e.HasKey(l => l.Id);
            e.Property(l => l.Id).HasColumnName("id");
            e.Property(l => l.UserId).HasColumnName("user_id");
            e.Property(l => l.SubmissionId).HasColumnName("submission_id");
            e.Property(l => l.BehaviorState).HasColumnName("behavior_state");
            e.Property(l => l.HelplessnessScoreDelta).HasColumnName("helplessness_score_delta");
            e.Property(l => l.CumulativeHelplessnessScore).HasColumnName("cumulative_helplessness_score");
            e.Property(l => l.MasteryProbability).HasColumnName("mastery_probability");
            e.Property(l => l.InterventionTriggered).HasColumnName("intervention_triggered");
            e.Property(l => l.DiagnosticCategory).HasColumnName("diagnostic_category");
            e.Property(l => l.SkillType).HasColumnName("skill_type");
            e.Property(l => l.Timestamp).HasColumnName("timestamp");
            e.HasIndex(l => l.UserId);
            e.HasIndex(l => l.Timestamp);
        });

        // ── scaffolding_hints → ScaffoldingHint ──
        modelBuilder.Entity<ScaffoldingHint>(e =>
        {
            e.ToTable("scaffolding_hints");
            e.HasKey(h => h.Id);
            e.Property(h => h.Id).HasColumnName("id");
            e.Property(h => h.DiagnosticCategory).HasColumnName("diagnostic_category");
            e.Property(h => h.SkillType).HasColumnName("skill_type");
            e.Property(h => h.Tier).HasColumnName("tier");
            e.Property(h => h.NpcName).HasColumnName("npc_name");
            e.Property(h => h.DialogueText).HasColumnName("dialogue_text");
            e.Property(h => h.TechnicalHint).HasColumnName("technical_hint");
            e.Property(h => h.IsActive).HasColumnName("is_active");
            e.HasIndex(h => new { h.DiagnosticCategory, h.SkillType, h.Tier });
        });

        // ── keystroke_raw_events → KeystrokeRawEventBatch ──
        modelBuilder.Entity<KeystrokeRawEventBatch>(e =>
        {
            e.ToTable("keystroke_raw_events");
            e.HasKey(k => k.Id);
            e.Property(k => k.Id).HasColumnName("id");
            e.Property(k => k.SubmissionId).HasColumnName("submission_id");
            e.Property(k => k.UserId).HasColumnName("user_id");
            e.Property(k => k.SessionId).HasColumnName("session_id");
            e.Property(k => k.Events).HasColumnName("events").HasColumnType("jsonb");
            e.Property(k => k.CapturedAt).HasColumnName("captured_at");
            e.HasIndex(k => k.SubmissionId);
            e.HasIndex(k => k.UserId);
        });

        // ── puzzles → Puzzle ──
        modelBuilder.Entity<Puzzle>(e =>
        {
            e.ToTable("puzzles");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.Title).HasColumnName("title");
            e.Property(p => p.Description).HasColumnName("description");
            e.Property(p => p.DungeonLevel).HasColumnName("dungeon_level");
            e.Property(p => p.OrderIndex).HasColumnName("order_index");
            e.Property(p => p.SkillType).HasColumnName("skill_type");
            e.Property(p => p.StarterCode).HasColumnName("starter_code");
            e.Property(p => p.ExpectedOutput).HasColumnName("expected_output");
            e.Property(p => p.ArrayConcept).HasColumnName("array_concept");
            e.Property(p => p.IsActive).HasColumnName("is_active");
            e.Property(p => p.TestCases).HasColumnName("test_cases").HasColumnType("jsonb");
            e.HasIndex(p => new { p.DungeonLevel, p.OrderIndex });
        });
    }
}
