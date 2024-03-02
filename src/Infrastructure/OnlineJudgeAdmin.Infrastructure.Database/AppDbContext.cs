using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

namespace OnlineJudgeAdmin.Infrastructure.Database;

public class AppDbContext : DbContext
{
    public DbSet<DbProblem> Problems { get; set; }
    public DbSet<DbProblemTag> ProblemsTags { get; set; }
    public DbSet<DbTag> Tags { get; set; }


    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbProblemTag>()
            .HasKey(pt => new { pt.ProblemId, pt.TagId });

        modelBuilder.Entity<DbProblemTag>()
            .HasOne(pt => pt.Problem)
            .WithMany(p => p.ProblemTags)
            .HasForeignKey(pt => pt.ProblemId);

        modelBuilder.Entity<DbProblemTag>()
            .HasOne(pt => pt.Tag)
            .WithMany(t => t.ProblemTags)
            .HasForeignKey(pt => pt.TagId);
    }
}