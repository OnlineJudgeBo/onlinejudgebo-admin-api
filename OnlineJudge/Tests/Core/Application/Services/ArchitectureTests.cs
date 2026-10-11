using System.Reflection;
using Microsoft.AspNetCore.Mvc;

// Keeps the onion: dependencies only point inwards, and Core stays free of infrastructure.
// A failure here means a layer started depending on something it must not; see README, "Arquitectura".
public class ArchitectureTests
{
    private const string Models = "OnlineJudgeAdmin.Core.Domain.Models";
    private const string Abstractions = "OnlineJudgeAdmin.Core.Domain.Abstractions";

    // Network, mail, web, database and cloud libraries: only Infrastructure and Presentation may use them.
    private static readonly string[] InfrastructureLibraries =
    [
        "System.Net.Http", "System.Net.Mail", "System.Data", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore",
        "Microsoft.Extensions.Http", "MySql", "Pomelo", "AWSSDK", "AutoMapper",
    ];

    private static readonly Assembly[] Projects = Directory.GetFiles(AppContext.BaseDirectory, "OnlineJudgeAdmin*.dll")
        .Where(file => !file.EndsWith(".Tests.dll", StringComparison.Ordinal))
        .Select(Assembly.LoadFrom)
        .ToArray();

    private static string[] References(Assembly project) =>
        project.GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();

    private static bool Allowed(string project, string reference) => project switch
    {
        Models => false,
        Abstractions => reference == Models,
        _ when project.StartsWith("OnlineJudgeAdmin.Core.", StringComparison.Ordinal) => reference.StartsWith("OnlineJudgeAdmin.Core.", StringComparison.Ordinal),
        _ when project.StartsWith("OnlineJudgeAdmin.Infrastructure.", StringComparison.Ordinal) => reference is Models or Abstractions,
        _ => true,
    };

    [Fact]
    public void EveryLayerIsLoaded()
    {
        var names = Projects.Select(project => project.GetName().Name).ToArray();

        Assert.Contains(Models, names);
        Assert.Contains(Abstractions, names);
        Assert.Contains("OnlineJudgeAdmin.Core.Application.Services", names);
        Assert.Contains("OnlineJudgeAdmin.Infrastructure.Database", names);
        Assert.Contains("OnlineJudgeAdminApi", names);
    }

    [Fact]
    public void ProjectsOnlyDependOnInnerLayers()
    {
        var broken = from project in Projects
                     let name = project.GetName().Name!
                     from reference in References(project)
                     where reference.StartsWith("OnlineJudgeAdmin", StringComparison.Ordinal) && !Allowed(name, reference)
                     select $"{name} -> {reference}";

        Assert.Empty(broken);
    }

    [Fact]
    public void CoreDoesNotUseInfrastructureLibraries()
    {
        var broken = from project in Projects
                     let name = project.GetName().Name!
                     where name.StartsWith("OnlineJudgeAdmin.Core.", StringComparison.Ordinal)
                     from reference in References(project)
                     where InfrastructureLibraries.Any(library => reference.StartsWith(library, StringComparison.Ordinal))
                     select $"{name} -> {reference}";

        Assert.Empty(broken);
    }

    [Fact]
    public void ControllersOnlyTalkToServices()
    {
        var broken = from project in Projects
                     from type in project.GetTypes()
                     where typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract
                     from constructor in type.GetConstructors()
                     from parameter in constructor.GetParameters()
                     let dependency = parameter.ParameterType
                     where (dependency.Namespace ?? string.Empty).StartsWith(Abstractions + ".Repositories", StringComparison.Ordinal)
                         || (dependency.Namespace ?? string.Empty).StartsWith("OnlineJudgeAdmin.Infrastructure", StringComparison.Ordinal)
                         || (dependency.Namespace ?? string.Empty).StartsWith("OnlineJudgeAdmin.Core.Application", StringComparison.Ordinal)
                         || dependency == typeof(IHttpClientFactory)
                         || dependency == typeof(HttpClient)
                     select $"{type.Name} -> {dependency.Name}";

        Assert.Empty(broken);
    }
}
