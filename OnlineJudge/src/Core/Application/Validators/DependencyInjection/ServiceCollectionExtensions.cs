using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudgeAdmin.Core.Application.Validators.Implementations;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Validators.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationValidators(this IServiceCollection services)
    {
        services.AddScoped<IValidator<Problem>, ProblemValidator>();
        return services;
    }
}
