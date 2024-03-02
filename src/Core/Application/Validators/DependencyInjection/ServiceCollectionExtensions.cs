using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudgeAdmin.Core.Application.Validators.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using System.Reflection;


namespace OnlineJudgeAdmin.Core.Application.Validators.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDatabaseRepositories(this IServiceCollection services)
        {
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
            services.AddSingleton<IUsersRepository, UsersRepository>();
            services.AddApplicationValidators();

            return services;
        }

        public static IServiceCollection AddApplicationValidators(this IServiceCollection services)
        {
            services.AddSingleton<IValidator<User>, UserValidator>();

            return services;
        }
    }
}