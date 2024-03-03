using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

namespace OnlineJudgeAdmin.Infrastructure.Database;

public class AppDbContext : DbContext
{
    public DbSet<DbProblem> Problems { get; set; }
    public DbSet<DbTag> Tags { get; set; }


    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbProblem>()
            .HasMany(pt => pt.Tags)
            .WithMany(p => p.Problems)
            .UsingEntity<Dictionary<string, object>>(
                "problem_tags",
                j => j.HasOne<DbTag>().WithMany().HasForeignKey("tag_id"),
                j => j.HasOne<DbProblem>().WithMany().HasForeignKey("problem_id"),
                j =>
                {
                    j.ToTable("problem_tags");
                });
    }
}