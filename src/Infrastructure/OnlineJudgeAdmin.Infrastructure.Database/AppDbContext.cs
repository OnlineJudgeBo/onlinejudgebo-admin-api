using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

namespace OnlineJudgeAdmin.Infrastructure.Database;

public class AppDbContext : DbContext
{
    public DbSet<DbProblem> problems { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options)
         : base(options)
    {
    }
}