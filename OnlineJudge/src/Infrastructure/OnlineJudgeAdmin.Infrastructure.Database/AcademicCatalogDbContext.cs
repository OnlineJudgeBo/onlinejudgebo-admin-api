using Microsoft.EntityFrameworkCore;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

public partial class AcademicCatalogDbContext : DbContext
{
    public AcademicCatalogDbContext(DbContextOptions<AcademicCatalogDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DbAcademicTopic> Topics { get; set; }
    public virtual DbSet<DbLearningPath> LearningPaths { get; set; }
    public virtual DbSet<DbLearningPathTopic> LearningPathTopics { get; set; }
    public virtual DbSet<DbLearningPathProgress> LearningPathProgresses { get; set; }
    public virtual DbSet<DbLearningPathTopicProgress> LearningPathTopicProgresses { get; set; }
    public virtual DbSet<DbSubtopic> Subtopics { get; set; }
    public virtual DbSet<DbSubtopicProblem> SubtopicProblems { get; set; }
    public virtual DbSet<DbSubtopicTag> SubtopicTags { get; set; }
    public virtual DbSet<DbProblemTag> ProblemTags { get; set; }
    public virtual DbSet<DbProblemTagMap> ProblemTagMaps { get; set; }
    public virtual DbSet<DbCourse> Courses { get; set; }
    public virtual DbSet<DbCourseUser> CourseUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<DbAcademicTopic>(entity =>
        {
            entity.ToTable("topic");
            entity.HasKey(e => e.TopicId);
            entity.HasIndex(e => e.TopicKey, "topic_key").IsUnique();
        });

        modelBuilder.Entity<DbLearningPath>(entity =>
        {
            entity.ToTable("learning_path");
            entity.HasKey(e => e.LearningPathId);
            entity.HasIndex(e => e.LearningPathKey, "learning_path_key").IsUnique();
        });

        modelBuilder.Entity<DbLearningPathTopic>(entity =>
        {
            entity.ToTable("learning_path_topic");
            entity.HasKey(e => new { e.LearningPathId, e.TopicId });
        });

        modelBuilder.Entity<DbLearningPathProgress>(entity =>
        {
            entity.ToTable("learning_path_progress");
            entity.HasKey(e => new { e.LearningPathId, e.UserId });
            entity.HasIndex(e => e.UserId, "idx_learning_path_progress_user");
        });

        modelBuilder.Entity<DbLearningPathTopicProgress>(entity =>
        {
            entity.ToTable("learning_path_topic_progress");
            entity.HasKey(e => new { e.LearningPathId, e.UserId, e.TopicId });
            entity.HasIndex(e => e.UserId, "idx_learning_path_topic_progress_user");
        });

        modelBuilder.Entity<DbSubtopic>(entity =>
        {
            entity.ToTable("subtopic");
            entity.HasKey(e => e.SubtopicId);
            entity.HasIndex(e => new { e.TopicId, e.SubtopicKey }, "topic_subtopic_key").IsUnique();
        });

        modelBuilder.Entity<DbSubtopicProblem>(entity =>
        {
            entity.ToTable("subtopic_problem");
            entity.HasKey(e => new { e.SubtopicId, e.ProblemId });
        });

        modelBuilder.Entity<DbSubtopicTag>(entity =>
        {
            entity.ToTable("subtopic_tag");
            entity.HasKey(e => new { e.SubtopicId, e.TagId });
        });

        modelBuilder.Entity<DbProblemTag>(entity =>
        {
            entity.ToTable("problem_tag");
            entity.HasKey(e => e.TagId);
            entity.HasIndex(e => e.TagKey, "tag_key").IsUnique();
        });

        modelBuilder.Entity<DbProblemTagMap>(entity =>
        {
            entity.ToTable("problem_tag_map");
            entity.HasKey(e => new { e.ProblemId, e.TagId });
            entity.HasIndex(e => e.TagId, "idx_ptm_tag");
        });

        modelBuilder.Entity<DbCourse>(entity =>
        {
            entity.ToTable("course");
            entity.HasKey(e => e.CourseId);
            entity.HasIndex(e => e.CourseKey, "course_key").IsUnique();
            entity.HasIndex(e => e.InviteCode, "uk_course_invite_code").IsUnique();
            entity.HasIndex(e => e.LearningPathId, "fk_course_path");
        });

        modelBuilder.Entity<DbCourseUser>(entity =>
        {
            entity.ToTable("course_user");
            entity.HasKey(e => new { e.CourseId, e.UserId });
            entity.HasIndex(e => e.UserId, "idx_course_user_user");
        });

        OnModelCreatingAcademicPartial(modelBuilder);
    }

    partial void OnModelCreatingAcademicPartial(ModelBuilder modelBuilder);
}
