using Microsoft.EntityFrameworkCore;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

public partial class ScheduleManagementDbContext : DbContext
{
    public ScheduleManagementDbContext()
    {
    }

    public ScheduleManagementDbContext(DbContextOptions<ScheduleManagementDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DbSchedule> Schedules { get; set; }
    public virtual DbSet<DbTeacher> Teachers { get; set; }
    public virtual DbSet<DbSubject> Subjects { get; set; }
    public virtual DbSet<DbSubjectAssistant> SubjectAssistants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_uca1400_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<DbSchedule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");
            entity.ToTable("schedules");

            entity.HasIndex(e => e.TeacherId, "teacher_id");
            entity.HasIndex(e => e.SubjectId, "fk_schedules_subjects");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");

            entity.Property(e => e.DayOfWeek)
                .HasMaxLength(10)
                .HasColumnName("day_of_week");

            entity.Property(e => e.StartTime)
                .HasColumnType("time")
                .HasColumnName("start_time");

            entity.Property(e => e.EndTime)
                .HasColumnType("time")
                .HasColumnName("end_time");

            entity.Property(e => e.TeacherId)
                .HasColumnType("int(11)")
                .HasColumnName("teacher_id");

            entity.Property(e => e.SubjectId)
                .HasColumnType("int(11)")
                .HasColumnName("subject_id");

            entity.HasOne(d => d.Teacher)
                .WithMany(p => p.Schedules)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("schedules_ibfk_1");

            entity.HasOne(d => d.Subject)
                .WithMany(p => p.Schedules)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_schedules_subjects");
        });

        modelBuilder.Entity<DbTeacher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");
            entity.ToTable("teachers");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");

            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<DbSubject>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");
            entity.ToTable("subjects");

            entity.HasIndex(e => e.Name, "name").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");

            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<DbSubjectAssistant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");
            entity.ToTable("assistants");

            entity.HasIndex(e => e.SubjectId, "idx_assistants_subject_id");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");

            entity.Property(e => e.SubjectId)
                .HasColumnType("int(11)")
                .HasColumnName("subject_id");

            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");

            entity.Property(e => e.Schedule)
                .HasMaxLength(255)
                .HasColumnName("schedule");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
