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
        public static IServiceCollection AddScheduleDatabaseRepositories(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAutoMapper(_ => { }, Assembly.GetExecutingAssembly());
            services.AddScoped<IScheduleManagerRepository, ScheduleManagerRepository>();

            services.AddMysqlClient(configuration);
            return services;
        }

        private static IServiceCollection AddMysqlClient(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("ScheduleConnection");
            var mysqlMajor = configuration.GetConnectionString("MysqlMajor");
            var mysqlMinor = configuration.GetConnectionString("MysqlMinor");
            var mysqlBuild = configuration.GetConnectionString("MysqlBuild");

            services.AddDbContext<ScheduleDbContext>(options =>
                options.UseMySql(connectionString, new MySqlServerVersion(new Version(int.Parse(mysqlMajor), int.Parse(mysqlMinor), int.Parse(mysqlBuild)))));

            return services;
        }

    }
}
