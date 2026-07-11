using System.Xml.Linq;

namespace OnlineJudgeAdmin.Core.Application.Services.Tests;

public class ProjectReferenceTests
{
    [Fact]
    public void Core_projects_do_not_reference_outer_layers()
    {
        var root = FindRoot();
        var coreProjects = new[]
        {
            "OnlineJudge/src/Core/Domain",
            "OnlineJudge/src/Core/Application"
        }
            .SelectMany(pathPart => Directory.GetFiles(
                Path.Combine(root, pathPart),
                "*.csproj",
                SearchOption.AllDirectories))
            .ToArray();

        var violations = coreProjects
            .SelectMany(projectFile => ProjectReferences(projectFile)
                .Select(reference => new
                {
                    ProjectFile = projectFile,
                    ReferencePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFile)!, reference))
                }))
            .Where(reference =>
                IsInFolder(reference.ReferencePath, root, "OnlineJudge/src/Infrastructure") ||
                IsInFolder(reference.ReferencePath, root, "OnlineJudge/src/Presentation"))
            .Select(reference =>
                $"{Path.GetRelativePath(root, reference.ProjectFile)} -> {Path.GetRelativePath(root, reference.ReferencePath)}")
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> ProjectReferences(string projectFile)
    {
        return XDocument.Load(projectFile)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))!;
    }

    private static bool IsInFolder(string path, string root, string folder)
    {
        var relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
        return relativePath.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OnlineJudgeAdmin.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing OnlineJudgeAdmin.sln.");
    }
}
