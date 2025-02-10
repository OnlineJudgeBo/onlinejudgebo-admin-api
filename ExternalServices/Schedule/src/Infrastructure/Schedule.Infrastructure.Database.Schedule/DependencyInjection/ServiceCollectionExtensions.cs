using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Infrastructure.Database.Schedule.Implementations;
using System.Reflection;

namespace OnlineJudgeAdmin.Infrastructure.Database.Schedule.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddScheduleDatabaseRepositories(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
            services.AddScoped<IScheduleRepository, ScheduleRepository>();

            services.AddMysqlClient(configuration);
            return services;
        }

        private static IServiceCollection AddMysqlClient(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("ScheduleConnection");
            var mysqlMajor = configuration.GetConnectionString("MysqlMajor");
            var mysqlMinor = configuration.GetConnectionString("MysqlMinor");
            var mysqlBuild = configuration.GetConnectionString("MysqlBuild");

            services.AddDbContext<DbContext>(options =>
                options.UseMySql(connectionString, new MySqlServerVersion(new Version(int.Parse(mysqlMajor), int.Parse(mysqlMinor), int.Parse(mysqlBuild)))));

            return services;
        }

    }
}
