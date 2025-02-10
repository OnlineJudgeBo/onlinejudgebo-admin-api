using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ScheduleManager.Core.Application.Validators.Implementations;
using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Application.Validators.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationValidators(this IServiceCollection services)
    {
        services.AddScoped<IValidator<Problem>, ProblemValidator>();
        return services;
    }
}
