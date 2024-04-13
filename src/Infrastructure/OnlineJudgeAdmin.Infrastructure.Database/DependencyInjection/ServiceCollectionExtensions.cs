using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
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

            services.AddMysqlClient(configuration);
            return services;
        }

        private static IServiceCollection AddMysqlClient(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<AppDbContext>(options =>
                  options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21))));

            return services;
        }
    }
}
