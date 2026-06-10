using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using System.Reflection;

namespace OnlineJudgeAdmin.Infrastructure.Database.DependencyInjection
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
            services.AddScoped<IAcademicRepository, AcademicRepository>();
            services.AddScoped<IPublicRepository, PublicRepository>();

            services.AddMysqlClient(configuration);
            return services;
        }

        private static IServiceCollection AddMysqlClient(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = GetRequiredConnectionSetting(configuration, "DefaultConnection");
            var academicConnectionString = ResolveAcademicConnectionString(configuration, connectionString);
            var mysqlMajor = GetRequiredConnectionSetting(configuration, "MysqlMajor");
            var mysqlMinor = GetRequiredConnectionSetting(configuration, "MysqlMinor");
            var mysqlBuild = GetRequiredConnectionSetting(configuration, "MysqlBuild");

            var mysqlVersion = new MySqlServerVersion(new Version(
                int.Parse(mysqlMajor),
                int.Parse(mysqlMinor),
                int.Parse(mysqlBuild)));

            services.AddDbContext<AppDbContext>(options =>
                options.UseMySql(connectionString, mysqlVersion, mySqlOptions =>
                    mySqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

            services.AddDbContext<AcademicCatalogDbContext>(options =>
                options.UseMySql(academicConnectionString, mysqlVersion, mySqlOptions =>
                    mySqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

            return services;
        }

        private static string GetRequiredConnectionSetting(IConfiguration configuration, string key)
        {
            return configuration.GetConnectionString(key)
                ?? throw new InvalidOperationException($"{key} is not configured.");
        }

        private static string ResolveAcademicConnectionString(IConfiguration configuration, string defaultConnectionString)
        {
            var configuredAcademicConnection = configuration.GetConnectionString("AcademicConnection");
            if (!string.IsNullOrWhiteSpace(configuredAcademicConnection))
            {
                return configuredAcademicConnection;
            }

            var builder = new MySqlConnectionStringBuilder(defaultConnectionString)
            {
                Database = "academic"
            };

            return builder.ConnectionString;
        }
    }
}
