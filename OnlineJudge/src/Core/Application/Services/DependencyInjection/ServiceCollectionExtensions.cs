using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScheduleManager.Core.Application.Services.Implementations;
using ScheduleManager.Core.Domain.Abstractions.Services;

namespace ScheduleManager.Core.Application.Services.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IProblemService, ProblemService>();
        services.AddScoped<ITopicService, TopicService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IContestService, ContestService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IProgrammingLanguageService, ProgrammingLanguageService>();
        services.AddScoped<IFileManagerService, FileManagerService>();
        services.AddScoped<IStatiscService, StaticsServices>();
        services.AddScoped<IJudgeService, JudgeService>();
        services.AddScoped<ISolutionService, SolutionService>();
        return services;
    }
}
