using Microsoft.Extensions.Configuration;
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

    public static IServiceCollection AddControlServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(ControlServerHttpClient.ClientName, client =>
        {
            var url = configuration["ControlServer:Url"];
            if (!string.IsNullOrWhiteSpace(url))
            {
                client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
            }
        });
        services.AddSingleton<IControlGroupStore, ControlGroupHttpStore>();
        services.AddSingleton<IControlServerClient, ControlServerHttpClient>();
        return services;
    }
}
