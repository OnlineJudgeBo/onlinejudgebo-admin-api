using Microsoft.Extensions.DependencyInjection;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

namespace OnlineJudgeAdmin.Infrastructure.Http.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHttpInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IChatCompletionClient, OpenRouterChatClient>();
        services.AddSingleton<IRemoteFileReader, HttpRemoteFileReader>();
        return services;
    }
}
