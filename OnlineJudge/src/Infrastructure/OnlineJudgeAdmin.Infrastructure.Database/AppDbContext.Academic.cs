using Microsoft.EntityFrameworkCore;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

public partial class AppDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbSolution>(entity =>
        {
            entity.HasIndex(e => e.SiteId, "idx_solution_site_id");

            entity.Property(e => e.SiteId)
                .HasColumnType("int(11)")
                .HasColumnName("site_id")
                .HasDefaultValue(1);
        });
    }
}

public partial class AcademicCatalogDbContext
{
    public virtual DbSet<DbCourseAssignment> CourseAssignments { get; set; }
    public virtual DbSet<DbCourseAssignmentProblem> CourseAssignmentProblems { get; set; }
    public virtual DbSet<DbCourseSubmissionContext> CourseSubmissionContexts { get; set; }
    public virtual DbSet<DbCourseContentItem> CourseContentItems { get; set; }

    partial void OnModelCreatingAcademicPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbCourseAssignment>(entity =>
        {
            entity.ToTable("course_assignment");
            entity.HasKey(e => e.AssignmentId);
            entity.HasIndex(e => e.CourseId, "idx_course_assignment_course");
        });

        modelBuilder.Entity<DbCourseAssignmentProblem>(entity =>
        {
            entity.ToTable("course_assignment_problem");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AssignmentId, "idx_course_assignment_problem_assignment");
            entity.HasIndex(e => e.ProblemId, "idx_course_assignment_problem_problem");
            entity.HasIndex(e => new { e.AssignmentId, e.ProblemId }, "uk_course_assignment_problem").IsUnique();
        });

        modelBuilder.Entity<DbCourseSubmissionContext>(entity =>
        {
            entity.ToTable("course_submission_context");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SolutionId, "idx_course_submission_solution");
            entity.HasIndex(e => e.CourseId, "idx_course_submission_course");
            entity.HasIndex(e => e.AssignmentId, "idx_course_submission_assignment");
            entity.HasIndex(e => e.UserId, "idx_course_submission_user");
        });

        modelBuilder.Entity<DbCourseContentItem>(entity =>
        {
            entity.ToTable("course_content_item");
            entity.HasKey(e => e.ItemId);
            entity.HasIndex(e => new { e.CourseId, e.Position }, "idx_course_content_order");
            entity.HasIndex(e => e.AssignmentId, "uk_course_content_assignment").IsUnique();
        });
    }
}
