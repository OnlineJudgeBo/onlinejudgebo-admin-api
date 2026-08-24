using System.Text.RegularExpressions;
using FluentValidation;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class ProblemService : IProblemService
{
    private readonly IProblemRepository _problemRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IPrivilegeRepository _privilegeRepository;
    private readonly IFileSystemLocalManagerManager _FileSystemLocalManagerManager;
    private readonly IValidator<Problem> _userValidation;

    public ProblemService(
        IProblemRepository problemRepository,
        ITopicRepository topicRepository,
        IPrivilegeRepository privilegeRepository,
        IFileSystemLocalManagerManager FileSystemLocalManagerManager,
        IValidator<Problem> ProblemValidation)
    {
        _problemRepository = problemRepository ?? throw new ArgumentNullException(nameof(problemRepository));
        _topicRepository = topicRepository ?? throw new ArgumentNullException(nameof(topicRepository));
        _privilegeRepository = privilegeRepository ?? throw new ArgumentNullException(nameof(privilegeRepository));
        _FileSystemLocalManagerManager = FileSystemLocalManagerManager ?? throw new ArgumentNullException(nameof(FileSystemLocalManagerManager));
        _userValidation = ProblemValidation ?? throw new ArgumentNullException(nameof(ProblemValidation));
    }

    public async Task<IEnumerable<Problem>> GetAllProblemsAsync(CurrentUser currentUser)
    {
        if (currentUser.Role == UserRolesEnum.Administrador
            || currentUser.Role == UserRolesEnum.Docente
            || currentUser.Role == UserRolesEnum.Auxiliar)
        {
            return await _problemRepository.GetAllProblemsForAdminAsync(currentUser.SiteId);
        }
        else
        {
            return await _problemRepository.GetAllProblemsAsync(currentUser.SiteId);
        }
    }

    public async Task<Problem> GetProblemByIdAsync(int problemId, int? siteId = null)
    {
        return await _problemRepository.GetProblemByIdAsync(problemId, siteId);
    }

    public async Task<IEnumerable<Problem>> SearchProblemAsync(CurrentUser currentUser, string searchTerm)
    {
        if (currentUser.Role == UserRolesEnum.Administrador
            || currentUser.Role == UserRolesEnum.Docente
            || currentUser.Role == UserRolesEnum.Auxiliar)
        {
            return await _problemRepository.SearchProblemForAdminAsync(searchTerm, currentUser.SiteId);
        }
        else
        {
            return await _problemRepository.SearchProblemAsync(searchTerm, currentUser.SiteId);
        }
    }

    public async Task<Problem> CreateProblemAsync(string userId, Problem problem, int siteId)
    {
        problem.OriginSource = string.IsNullOrWhiteSpace(problem.OriginSource)
            ? "General"
            : string.Join(' ', problem.OriginSource.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        IEnumerable<Classification>? newTopic = problem.Classifications;
        problem.Classifications = null;
        NumberSampleCases(problem);

        Problem newProblem = await _problemRepository.CreateProblemAsync(problem, siteId);
        if (newTopic != null)
        {
            await _topicRepository.AddClassificationsToProblemAsync(newProblem.ProblemId.Value, newTopic);
        }

        _FileSystemLocalManagerManager.CreateFolder(newProblem.ProblemId.Value.ToString());
        SyncSampleCaseFiles(newProblem.ProblemId.Value.ToString(), problem);

        Privilege privilege = new Privilege();
        privilege.UserId = userId;
        privilege.Rightstr = "p" + newProblem.ProblemId;
        _privilegeRepository.CreatePrivilegeAsync(privilege);
        return newProblem;
    }

    public async Task<Problem> UpdateProblemAsync(string userId, int problemId, Problem problem, int siteId)
    {
        var existingProblem = await _problemRepository.GetProblemByIdAsync(problemId, siteId);
        //ValidateUser(user);

        if (userId == string.Empty)
        {
            throw new ArgumentNullException(nameof(userId));
        }

        if (existingProblem == null)
        {
            throw new ApplicationException("Problem does not exist.");
        }

        problem.OriginSource = string.IsNullOrWhiteSpace(problem.OriginSource)
            ? "General"
            : string.Join(' ', problem.OriginSource.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        await _topicRepository.RemoveAllClassificationsFromProblemAsync(existingProblem.ProblemId.Value);
        IEnumerable<Classification>? newTopic = problem.Classifications;
        problem.Classifications = null;
        NumberSampleCases(problem);

        var updateProblem = await _problemRepository.UpdateProblemAsync(userId, problemId, problem, siteId);
        if (updateProblem != null)
        {
            await _topicRepository.AddClassificationsToProblemAsync(updateProblem.ProblemId.Value, newTopic);
        }

        _FileSystemLocalManagerManager.CreateFolder(updateProblem.ProblemId.Value.ToString());
        _FileSystemLocalManagerManager.CreateFolder(updateProblem.ProblemId.Value.ToString() + "/ac");
        SyncSampleCaseFiles(updateProblem.ProblemId.Value.ToString(), problem);

        return updateProblem;
    }

    // Writes one .in/.out pair per sample case so every case is visible/manageable from the
    // admin file explorer, not just the first one. "sample.in"/"sample.out" (no number) stays
    // the first case's filename for backward compatibility; extra cases get "sample-N.in/out".
    // Also deletes any leftover "sample-N.*" from a previously larger sample set.
    private void SyncSampleCaseFiles(string problemFolder, Problem problem)
    {
        var samples = (problem.SampleCases?.Count > 0
            ? problem.SampleCases.OrderBy(s => s.Num)
            : Enumerable.Empty<ProblemSample>()).ToList();

        var firstInput = samples.Count > 0 ? samples[0].Input : problem.SampleInput;
        var firstOutput = samples.Count > 0 ? samples[0].Output : problem.SampleOutput;
        _FileSystemLocalManagerManager.WriteToFile(problemFolder, "sample.in", firstInput ?? string.Empty);
        _FileSystemLocalManagerManager.WriteToFile(problemFolder, "sample.out", firstOutput ?? string.Empty);

        foreach (var sample in samples.Skip(1))
        {
            _FileSystemLocalManagerManager.WriteToFile(problemFolder, $"sample-{sample.Num}.in", sample.Input ?? string.Empty);
            _FileSystemLocalManagerManager.WriteToFile(problemFolder, $"sample-{sample.Num}.out", sample.Output ?? string.Empty);
        }

        var existingFiles = _FileSystemLocalManagerManager.ListFiles(problemFolder) ?? Array.Empty<string>();
        foreach (var fileName in existingFiles)
        {
            var match = SampleFileNamePattern.Match(fileName);
            if (match.Success && int.Parse(match.Groups["num"].Value) > samples.Count)
            {
                _FileSystemLocalManagerManager.DeleteFile(problemFolder, fileName);
            }
        }
    }

    private static readonly Regex SampleFileNamePattern = new(@"^sample-(?<num>\d+)\.(in|out)$", RegexOptions.Compiled);

    private static void NumberSampleCases(Problem problem)
    {
        if (problem.SampleCases == null || problem.SampleCases.Count == 0)
        {
            return;
        }

        int num = 1;
        foreach (var sample in problem.SampleCases)
        {
            sample.Num = num++;
        }

        var first = problem.SampleCases.First();
        problem.SampleInput = first.Input;
        problem.SampleOutput = first.Output;
    }

    public async Task ChangeProblemVisibilityAsync(int problemId, int siteId)
    {
        await _problemRepository.ChangeProblemVisibilityAsync(problemId, siteId);
    }

    public async Task DeleteProblemAsync(int problemId, int siteId)
    {
        await _problemRepository.DeleteProblemAsync(problemId, siteId);
    }
}
