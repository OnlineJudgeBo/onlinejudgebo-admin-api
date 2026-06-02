using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    public virtual DbSet<DbClassification> Classifications { get; set; }
    public virtual DbSet<DbContest> Contests { get; set; }
    public virtual DbSet<DbContestProblem> ContestProblems { get; set; }
    public virtual DbSet<DbContestUser> ContestUsers { get; set; }
    public virtual DbSet<DbNews> News { get; set; }
    public virtual DbSet<DbPrivilege> Privilege { get; set; }
    public virtual DbSet<DbProblem> Problems { get; set; }
    public virtual DbSet<DbProgrammingLanguage> ProgrammingLanguages { get; set; }
    public virtual DbSet<DbRole> Roles { get; set; }
    public virtual DbSet<DbSolution> Solutions { get; set; }
    public virtual DbSet<DbSourceCode> SourceCodes { get; set; }
    public virtual DbSet<DbTopic> Topics { get; set; }
    public virtual DbSet<DbUser> Users { get; set; }
    public virtual DbSet<DbUserActivity> UserActivities { get; set; }
    public virtual DbSet<DbUserProfile> UserProfiles { get; set; }
    public virtual DbSet<DbUserRole> UserRoles { get; set; }
    public virtual DbSet<DbUserSetting> UserSettings { get; set; }
    public virtual DbSet<DbMonthlySubmission> DbMonthlySubmission { get; set; }
    public virtual DbSet<DbLanguageSubmission> DbLanguageSubmission { get; set; }
    public virtual DbSet<DbRemoteClient> DbRemoteClient { get; set; }
    public virtual DbSet<DbSolutionClient> DbSolutionClient { get; set; }
    public virtual DbSet<DbContestSite> ContestSites { get; set; }
    public virtual DbSet<DbSite> Sites { get; set; }
    public virtual DbSet<DbProblemSite> ProblemSites { get; set; }

    private static readonly int[] value = new[] { 0, 0 };

    protected override void OnModelCreating(ModelBuilder modelBuilder)

    {
        modelBuilder.Entity<DbMonthlySubmission>().HasNoKey();
        modelBuilder.Entity<DbLanguageSubmission>().HasNoKey();


        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<DbClassification>(entity =>
        {
            entity.HasKey(e => e.ClassificationId).HasName("PRIMARY");

            entity
                .ToTable("classification")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.TopicId, "topic_id");

            entity.Property(e => e.ClassificationId)
                .HasColumnType("int(11)")
                .HasColumnName("classification_id");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.TopicId)
                .HasColumnType("int(11)")
                .HasColumnName("topic_id");

            entity.HasOne(d => d.Topic).WithMany(p => p.Classifications)
                .HasForeignKey(d => d.TopicId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("classification_ibfk_1");
        });

        modelBuilder.Entity<DbCompileinfo>(entity =>
        {
            entity.HasKey(e => e.SolutionId).HasName("PRIMARY");

            entity
                .ToTable("compileinfo")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.SolutionId)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("solution_id");
            entity.Property(e => e.Error)
                .HasColumnType("text")
                .HasColumnName("error");

            entity.HasOne(d => d.Solution).WithOne(p => p.Compileinfo)
                .HasForeignKey<DbCompileinfo>(d => d.SolutionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("compileinfo_ibfk_1");
        });

        modelBuilder.Entity<DbContest>(entity =>
        {
            entity.HasKey(e => e.ContestId).HasName("PRIMARY");

            entity
                .ToTable("contest")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.ContestId)
                .HasColumnType("int(11)")
                .HasColumnName("contest_id");
            entity.Property(e => e.Defunct)
                .HasMaxLength(1)
                .HasDefaultValueSql("'N'")
                .IsFixedLength()
                .HasColumnName("defunct");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.EndTime)
                .HasColumnType("datetime")
                .HasColumnName("end_time");
            //.HasConversion(DateTimeConverter.ValueConverter);
            entity.Property(e => e.Langmask)
                .HasComment("bits for LANG to mask")
                .HasColumnType("int(11)")
                .HasColumnName("langmask");
            entity.Property(e => e.Level)
                .HasMaxLength(32)
                .HasDefaultValueSql("'PRACTICE'")
                .HasColumnName("level");
            entity.Property(e => e.Obi).HasColumnName("obi");
            entity.Property(e => e.Private)
                .HasColumnType("tinyint(4)")
                .HasColumnName("private");
            entity.Property(e => e.StartTime)
                .HasColumnType("datetime")
                .HasColumnName("start_time");
            entity.Property(e => e.Track)
                .HasMaxLength(32)
                .HasDefaultValueSql("'GENERAL'")
                .HasColumnName("track");
            //.HasConversion(DateTimeConverter.ValueConverter);
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
        });

        modelBuilder.Entity<DbContestProblem>(entity =>
        {
            entity.HasKey(e => new { e.ContestId, e.ProblemId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", value);

            entity
                .ToTable("contest_problem")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.ContestId, "contest_id");

            entity.HasIndex(e => e.ProblemId, "problem_id");

            entity.Property(e => e.ContestId)
                .HasColumnType("int(11)")
                .HasColumnName("contest_id");
            entity.Property(e => e.ProblemId)
                .HasColumnType("int(11)")
                .HasColumnName("problem_id");
            entity.Property(e => e.Num)
                .HasColumnType("int(11)")
                .HasColumnName("num");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("title");

            entity.HasOne(d => d.Contest).WithMany(p => p.ContestProblems)
                .HasForeignKey(d => d.ContestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("contest_problem_ibfk_1");

            entity.HasOne(d => d.Problem).WithMany(p => p.ContestProblems)
                .HasForeignKey(d => d.ProblemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("contest_problem_ibfk_2");
        });

        modelBuilder.Entity<DbSite>(entity =>
        {
            entity.HasKey(e => e.SiteId)
                .HasName("PRIMARY");

            entity.ToTable("sites")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_general_ci");

            entity.Property(e => e.SiteId)
                .HasColumnType("int(11)")
                .HasColumnName("site_id");

            entity.Property(e => e.Name)
                .HasColumnType("varchar(100)")
                .HasColumnName("name");
        });

        modelBuilder.Entity<DbContestSite>(entity =>
        {
            entity.HasKey(e => new { e.ContestId, e.SiteId })
                .HasName("PRIMARY");

            entity.ToTable("contest_site")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_general_ci");

            entity.HasIndex(e => e.ContestId, "contest_id");
            entity.HasIndex(e => e.SiteId, "site_id");

            entity.Property(e => e.ContestId)
                .HasColumnType("int(11)")
                .HasColumnName("contest_id")
                .IsRequired(true);

            entity.Property(e => e.SiteId)
                .HasColumnType("int(11)")
                .HasColumnName("site_id")
                .IsRequired(true);

            entity.HasOne(d => d.Contest)
                .WithMany(p => p.ContestSites)
                .HasForeignKey(d => d.ContestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("contest_site_ibfk_2");

            entity.HasOne(d => d.Site)
                .WithMany(p => p.ContestSites)
                .HasForeignKey(d => d.SiteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("contest_site_ibfk_1");
        });


        modelBuilder.Entity<DbProblemSite>(entity =>
        {
            entity.HasKey(e => new { e.problemId, e.SiteId })
                .HasName("PRIMARY");

            entity.ToTable("problems_site")
                .HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_general_ci");

            entity.HasIndex(e => e.problemId, "problem_id");
            entity.HasIndex(e => e.SiteId, "site_id");

            entity.Property(e => e.problemId)
                .HasColumnType("int(11)")
                .HasColumnName("problem_id")
                .IsRequired(true);

            entity.Property(e => e.SiteId)
                .HasColumnType("int(11)")
                .HasColumnName("site_id")
                .IsRequired(true);

            entity.HasOne(d => d.Problem)
                .WithMany(p => p.ProblemSites)
                .HasForeignKey(d => d.problemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("problem_site_ibfk_2");

            entity.HasOne(d => d.Site)
                .WithMany(p => p.ProblemSites)
                .HasForeignKey(d => d.SiteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("problem_site_ib,er4");
        });

        modelBuilder.Entity<DbLoginlog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("loginlog")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Ip)
                .HasMaxLength(100)
                .HasColumnName("ip");
            entity.Property(e => e.Password)
                .HasMaxLength(40)
                .HasColumnName("password");
            entity.Property(e => e.Time)
                .HasColumnType("datetime")
                .HasColumnName("time");
            entity.Property(e => e.UserId)
                .HasMaxLength(48)
                .HasDefaultValueSql("''")
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<DbNews>(entity =>
        {
            entity.HasKey(e => e.NewsId).HasName("PRIMARY");

            entity
                .ToTable("news")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.UserId, "user_id");

            entity.Property(e => e.NewsId)
                .HasColumnType("int(11)")
                .HasColumnName("news_id");
            entity.Property(e => e.Content)
                .HasColumnType("text")
                .HasColumnName("content");
            entity.Property(e => e.Defunct)
                .HasMaxLength(1)
                .HasDefaultValueSql("'N'")
                .IsFixedLength()
                .HasColumnName("defunct");
            entity.Property(e => e.Importance)
                .HasColumnType("tinyint(4)")
                .HasColumnName("importance");
            entity.Property(e => e.Time)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime")
                .HasColumnName("time");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasDefaultValueSql("''")
                .HasColumnName("title");
            entity.Property(e => e.UserId)
                .HasMaxLength(48)
                .HasDefaultValueSql("''")
                .HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.News)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("news_ibfk_1");
        });

        modelBuilder.Entity<DbOnline>(entity =>
        {
            entity.HasKey(e => e.Hash).HasName("PRIMARY");

            entity
                .ToTable("online")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_unicode_ci");

            entity.HasIndex(e => e.Hash, "hash").IsUnique();

            entity.Property(e => e.Hash)
                .HasMaxLength(32)
                .HasColumnName("hash");
            entity.Property(e => e.Firsttime)
                .HasColumnType("int(10)")
                .HasColumnName("firsttime");
            entity.Property(e => e.Ip)
                .HasMaxLength(20)
                .HasDefaultValueSql("''")
                .HasColumnName("ip")
                .UseCollation("utf8mb3_general_ci");
            entity.Property(e => e.Lastmove)
                .HasColumnType("int(10)")
                .HasColumnName("lastmove");
            entity.Property(e => e.Refer)
                .HasMaxLength(255)
                .HasColumnName("refer");
            entity.Property(e => e.Ua)
                .HasMaxLength(255)
                .HasDefaultValueSql("''")
                .HasColumnName("ua")
                .UseCollation("utf8mb3_general_ci");
            entity.Property(e => e.Uri)
                .HasMaxLength(255)
                .HasColumnName("uri");
        });

        modelBuilder.Entity<DbProblem>(entity =>
        {
            entity.HasKey(e => e.ProblemId).HasName("PRIMARY");

            entity
                .ToTable("problem")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.ProblemId)
                .HasColumnType("int(11)")
                .HasColumnName("problem_id");
            entity.Property(e => e.Accepted)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("accepted");
            entity.Property(e => e.Defunct)
                .HasMaxLength(1)
                .HasDefaultValueSql("'N'")
                .IsFixedLength()
                .HasColumnName("defunct");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Hint)
                .HasColumnType("text")
                .HasColumnName("hint");
            entity.Property(e => e.InDate)
                .HasColumnType("datetime")
                .HasColumnName("in_date");
            entity.Property(e => e.Input)
                .HasColumnType("text")
                .HasColumnName("input");
            entity.Property(e => e.MemoryLimit)
                .HasColumnType("int(11)")
                .HasColumnName("memory_limit");
            entity.Property(e => e.Output)
                .HasColumnType("text")
                .HasColumnName("output");
            entity.Property(e => e.SampleInput)
                .HasColumnType("text")
                .HasColumnName("sample_input");
            entity.Property(e => e.SampleOutput)
                .HasColumnType("text")
                .HasColumnName("sample_output");
            entity.Property(e => e.Solved)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("solved");
            entity.Property(e => e.Source)
                .HasMaxLength(100)
                .HasColumnName("source");
            entity.Property(e => e.OriginSource)
                .HasMaxLength(255)
                .HasColumnName("origin_source");
            entity.Property(e => e.Spj)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'")
                .IsFixedLength()
                .HasColumnName("spj");
            entity.Property(e => e.Submit)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("submit");
            entity.Property(e => e.TimeLimit)
                .HasColumnType("int(11)")
                .HasColumnName("time_limit");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasDefaultValueSql("''")
                .HasColumnName("title");

            entity.HasMany(d => d.Classifications).WithMany(p => p.Problems)
                .UsingEntity<Dictionary<string, object>>(
                    "ProblemClassification",
                    r => r.HasOne<DbClassification>().WithMany()
                        .HasForeignKey("ClassificationId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("problem_classification_ibfk_2"),
                    l => l.HasOne<DbProblem>().WithMany()
                        .HasForeignKey("ProblemId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("problem_classification_ibfk_1"),
                    j =>
                    {
                        j.HasKey("ProblemId", "ClassificationId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", value);
                        j.ToTable("problem_classification");
                        j.HasIndex(new[] { "ClassificationId" }, "classification_id");
                        j.IndexerProperty<int>("ProblemId")
                            .HasColumnType("int(11)")
                            .HasColumnName("problem_id");
                        j.IndexerProperty<int>("ClassificationId")
                            .HasColumnType("int(11)")
                            .HasColumnName("classification_id");
                    });
        });

        modelBuilder.Entity<DbRole>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PRIMARY");

            entity
                .ToTable("roles")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.RoleId)
                .HasColumnType("int(11)")
                .HasColumnName("role_id");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<DbRuntimeinfo>(entity =>
        {
            entity.HasKey(e => e.SolutionId).HasName("PRIMARY");

            entity
                .ToTable("runtimeinfo")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.SolutionId)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("solution_id");
            entity.Property(e => e.Error)
                .HasColumnType("text")
                .HasColumnName("error");

            entity.HasOne(d => d.Solution).WithOne(p => p.Runtimeinfo)
                .HasForeignKey<DbRuntimeinfo>(d => d.SolutionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("runtimeinfo_ibfk_1");
        });

        modelBuilder.Entity<DbSolution>(entity =>
        {
            entity.HasKey(e => e.SolutionId).HasName("PRIMARY");

            entity
                .ToTable("solution")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.ContestId, "cid");

            entity.HasIndex(e => e.ProblemId, "pid");

            entity.HasIndex(e => e.Result, "res");

            entity.HasIndex(e => e.UserId, "uid");

            entity.Property(e => e.SolutionId)
                .HasColumnType("int(11)")
                .HasColumnName("solution_id");
            entity.Property(e => e.CodeLength)
                .HasColumnType("int(11)")
                .HasColumnName("code_length");
            entity.Property(e => e.ContestId)
                .HasColumnType("int(11)")
                .HasColumnName("contest_id");
            entity.Property(e => e.InDate)
                .HasDefaultValueSql("'0000-00-00 00:00:00'")
                .HasColumnType("datetime")
                .HasColumnName("in_date");
            entity.Property(e => e.Ip)
                .HasMaxLength(15)
                .IsFixedLength()
                .HasColumnName("ip");
            entity.Property(e => e.Judgetime)
                .HasColumnType("datetime")
                .HasColumnName("judgetime");
            entity.Property(e => e.Language)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("language");
            entity.Property(e => e.Memory)
                .HasColumnType("int(11)")
                .HasColumnName("memory");
            entity.Property(e => e.Num)
                .HasDefaultValueSql("-1")
                .HasColumnType("tinyint(4)")
                .HasColumnName("num");
            entity.Property(e => e.PassRate)
                .HasColumnType("decimal(2,2) unsigned")
                .HasColumnName("pass_rate");
            entity.Property(e => e.ProblemId)
                .HasColumnType("int(11)")
                .HasColumnName("problem_id");
            entity.Property(e => e.RemoteId)
                .HasColumnType("int(11)")
                .HasColumnName("remote_id");
            entity.Property(e => e.Result)
                .HasColumnType("smallint(6)")
                .HasColumnName("result");
            entity.Property(e => e.Time)
                .HasColumnType("int(11)")
                .HasColumnName("time");
            entity.Property(e => e.UserId)
                .HasMaxLength(48)
                .HasColumnName("user_id");

            entity.HasOne(d => d.Problem).WithMany(p => p.Solutions)
                .HasForeignKey(d => d.ProblemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("solution_ibfk_2");

            entity.HasOne(d => d.User).WithMany(p => p.Solutions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("solution_ibfk_1");
        });

        modelBuilder.Entity<DbSourceCode>(entity =>
        {
            entity.HasKey(e => e.SolutionId).HasName("PRIMARY");

            entity
                .ToTable("source_code")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.SolutionId)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("solution_id");
            entity.Property(e => e.Source)
                .HasColumnType("text")
                .HasColumnName("source");

            entity.HasOne(d => d.Solution).WithOne(p => p.SourceCode)
                .HasForeignKey<DbSourceCode>(d => d.SolutionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("source_code_ibfk_1");
        });

        modelBuilder.Entity<DbTopic>(entity =>
        {
            entity.HasKey(e => e.TopicId).HasName("PRIMARY");

            entity
                .ToTable("topic")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.TopicId)
                .HasColumnType("int(11)")
                .HasColumnName("topic_id");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<DbUser>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity
                .ToTable("users")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.UserId, "user_id").IsUnique();

            entity.Property(e => e.UserId)
                .HasMaxLength(48)
                .HasDefaultValueSql("''")
                .HasColumnName("user_id");
            entity.Property(e => e.Accesstime)
                .HasColumnType("datetime")
                .HasColumnName("accesstime");
            entity.Property(e => e.Ip)
                .HasMaxLength(20)
                .HasColumnName("ip");
            entity.Property(e => e.Password)
                .HasMaxLength(32)
                .HasColumnName("password");
            entity.Property(e => e.RegTime)
                .HasColumnType("datetime")
                .HasColumnName("reg_time");
            entity.Property(e => e.ResetPasswordExpires)
                .HasColumnType("datetime")
                .HasColumnName("reset_password_expires");
            entity.Property(e => e.ResetPasswordToken)
                .HasMaxLength(255)
                .HasColumnName("reset_password_token");
            entity.Property(e => e.IsDeleted)
                .HasColumnName("is_deleted")
                .HasColumnType("tinyint(1)")
                .HasDefaultValue(false);
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasColumnType("tinyint(1)")
                .HasDefaultValue(true);
        });

        modelBuilder.Entity<DbUserActivity>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity
                .ToTable("user_activity")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.UserId, "user_id").IsUnique();

            entity.Property(e => e.UserId)
                .HasMaxLength(48)
                .HasDefaultValueSql("''")
                .HasColumnName("user_id");
            entity.Property(e => e.Solved)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("solved");
            entity.Property(e => e.Submit)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("submit");

            entity.HasOne(d => d.User).WithOne(p => p.UserActivity)
                .HasForeignKey<DbUserActivity>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_activity_ibfk_1");
        });

        modelBuilder.Entity<DbUserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity
                .ToTable("user_profiles")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.UserId, "user_id").IsUnique();

            entity.Property(e => e.UserId)
                .HasMaxLength(48)
                .HasDefaultValueSql("''")
                .HasColumnName("user_id");
            entity.Property(e => e.Ci)
                .HasColumnType("text")
                .HasColumnName("ci");
            entity.Property(e => e.Departament)
                .HasColumnType("int(11)")
                .HasColumnName("departament");
            entity.Property(e => e.District)
                .HasColumnType("text")
                .HasColumnName("district");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.Lastname)
                .HasMaxLength(15)
                .HasColumnName("lastname");
            entity.Property(e => e.Nick)
                .HasMaxLength(100)
                .HasDefaultValueSql("''")
                .HasColumnName("nick");
            entity.Property(e => e.PaisId)
                .HasDefaultValueSql("'0'")
                .HasColumnType("int(11)")
                .HasColumnName("pais_id");
            entity.Property(e => e.School)
                .HasMaxLength(100)
                .HasDefaultValueSql("''")
                .HasColumnName("school");

            entity.HasOne(d => d.User).WithOne(p => p.UserProfile)
                .HasForeignKey<DbUserProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_profiles_ibfk_1");
        });

        modelBuilder.Entity<DbUserSetting>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity
                .ToTable("user_settings")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.HasIndex(e => e.UserId, "user_id").IsUnique();

            entity.Property(e => e.UserId)
                .HasMaxLength(48)
                .HasDefaultValueSql("''")
                .HasColumnName("user_id");
            entity.Property(e => e.InstitucionId)
                .HasColumnType("int(11)")
                .HasColumnName("institucion_id");
            entity.Property(e => e.Language)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(11)")
                .HasColumnName("language");
            entity.Property(e => e.Level)
                .HasColumnType("int(1)")
                .HasColumnName("level");
            entity.Property(e => e.Obi)
                .HasColumnType("int(11)")
                .HasColumnName("obi");
            entity.Property(e => e.Rude)
                .HasColumnType("text")
                .HasColumnName("rude");
            entity.Property(e => e.Vcyt)
                .HasColumnType("text")
                .HasColumnName("vcyt");
            entity.Property(e => e.Volume)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(11)")
                .HasColumnName("volume");

            entity.HasOne(d => d.User).WithOne(p => p.UserSetting)
                .HasForeignKey<DbUserSetting>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_settings_ibfk_1");
        });


        modelBuilder.Entity<DbPrivilege>(entity =>
        {
            entity.HasKey(e => e.PrivilegeId).HasName("PRIMARY");

            entity
                .ToTable("privilege")
                .HasCharSet("utf8mb3")
                .UseCollation("utf8mb3_general_ci");

            entity.Property(e => e.PrivilegeId)
                .HasColumnType("int(11)")
                .HasColumnName("privilege_id");
            entity.Property(e => e.Defunct)
                .HasMaxLength(1)
                .HasDefaultValueSql("'N'")
                .IsFixedLength()
                .HasColumnName("defunct");
            entity.Property(e => e.Rightstr)
                .HasMaxLength(30)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("rightstr");
            entity.Property(e => e.UserId)
                .HasMaxLength(48)
                .HasDefaultValueSql("''")
                .IsFixedLength()
                .HasColumnName("user_id");
        });

        modelBuilder.Entity<DbContestUser>(entity =>
        {
            entity.HasKey(cu => new { cu.ContestId, cu.UserId, cu.SiteId });

            entity.HasOne(cu => cu.Contest)
                .WithMany(c => c.ContestUsers)
                .HasForeignKey(cu => cu.ContestId);

            entity.HasOne(cu => cu.User)
                .WithMany(u => u.ContestUsers)
                .HasForeignKey(cu => cu.UserId);

            entity.HasOne(cu => cu.Site)
                .WithMany(u => u.ContestUsers)
                .HasForeignKey(cu => cu.SiteId);
        });

        modelBuilder.Entity<DbContest>()
            .HasMany(c => c.ProgrammingLanguages)
            .WithMany(p => p.Contests)
            .UsingEntity<Dictionary<string, object>>(
                "contest_programming_language",
                j => j.HasOne<DbProgrammingLanguage>()
                    .WithMany()
                    .HasForeignKey("language_id")
                    .OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<DbContest>()
                    .WithMany()
                    .HasForeignKey("contest_id")
                    .OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.ToTable("contest_programming_language");
                }
            );

        modelBuilder.Entity<DbUserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(e => new { e.UserId, e.RoleId });

            entity.Property(e => e.RoleId)
                .HasColumnName("role_id")
                .HasColumnType("int(11)");

            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .HasMaxLength(48)
                .HasColumnType("varchar(48)");

            entity.HasOne(d => d.Role)
                .WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("user_roles_ibfk_2")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_roles_ibfk_1")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.RoleId);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<DbSolutionClient>(entity =>
        {
            entity.ToTable("solution_client");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .IsRequired()
                .ValueGeneratedOnAdd();

            entity.Property(e => e.SolutionId)
                .HasColumnName("solution_id")
                .IsRequired();

            entity.Property(e => e.ClientId)
                .HasColumnName("client_id")
                .IsRequired();

            entity.Property(e => e.AssignedDate)
                .HasColumnName("assigned_date")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<DbRemoteClient>(entity =>
        {
            entity.ToTable("remote_clients");

            entity.HasKey(e => e.ClientId);

            entity.Property(e => e.ClientId)
                .HasColumnName("client_id")
                .IsRequired()
                .ValueGeneratedOnAdd();

            entity.Property(e => e.CallbackUrl)
                .HasColumnName("callback_url")
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Token)
                .HasColumnName("token")
                .IsRequired();

            entity.Property(e => e.IsAvailable)
                .HasColumnName("is_available")
                .IsRequired();
        });

        OnModelCreatingPartial(modelBuilder);
    }
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

public class DateTimeConverter
{
    public static ValueConverter<DateTime?, DateTime?> Converter =
        new ValueConverter<DateTime?, DateTime?>(
            v => v, // Conversión al guardar en la base de datos: no se necesita cambio.
            v => v.HasValue && v.Value.Year < 2 ? null : v // Conversión al leer de la base de datos: maneja explícitamente los valores nulos.
        );
}
