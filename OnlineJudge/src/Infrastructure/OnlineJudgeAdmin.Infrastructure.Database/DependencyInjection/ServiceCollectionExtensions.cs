using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScheduleManager.Core.Domain.Abstractions.Repositories;
using ScheduleManager.Infrastructure.Database.Implementations;
using System.Reflection;

namespace ScheduleManager.Infrastructure.Database.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDatabaseRepositories(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
            services.AddScoped<IProblemRepository, ProblemRepository>();
            services.AddScoped<ITopicRepository, TopicRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IContestsRepository, ContestsRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IPrivilegeRepository, PrivilegeRepository>();
            services.AddScoped<IProgrammingLanguagesRepository, ProgrammingLanguageRepository>();
            services.AddScoped<IStaticsRepository, StaticsRepository>();
            services.AddScoped<IJudgeRepository, JudgeRepository>();
            services.AddScoped<ISolutionRepository, SolutionRepository>();
            services.AddScoped<ISolutionClientRepository, SolutionClientRepository>();

            services.AddMysqlClient(configuration);
            return services;
        }

        private static IServiceCollection AddMysqlClient(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var mysqlMajor = configuration.GetConnectionString("MysqlMajor");
            var mysqlMinor = configuration.GetConnectionString("MysqlMinor");
            var mysqlBuild = configuration.GetConnectionString("MysqlBuild");

            services.AddDbContext<AppDbContext>(options =>
                options.UseMySql(connectionString, new MySqlServerVersion(new Version(int.Parse(mysqlMajor), int.Parse(mysqlMinor), int.Parse(mysqlBuild)))));

            return services;
        }
    }
}
