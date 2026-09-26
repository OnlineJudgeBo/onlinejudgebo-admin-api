using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

// Each exam gets its own control-server group. Tokens are derived from ControlServer:TokenSecret,
// so nothing is stored: the same contest always maps to the same group and tokens.
public class ContestMachinesService : IContestMachinesService
{
    private readonly IContestsRepository _contestRepository;
    private readonly IPublicService _publicService;
    private readonly IControlGroupStore _groupStore;
    private readonly IConfiguration _configuration;

    public ContestMachinesService(
        IContestsRepository contestRepository,
        IPublicService publicService,
        IControlGroupStore groupStore,
        IConfiguration configuration)
    {
        _contestRepository = contestRepository ?? throw new ArgumentNullException(nameof(contestRepository));
        _publicService = publicService ?? throw new ArgumentNullException(nameof(publicService));
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    private int SiteId => int.TryParse(_configuration["ControlServer:SiteId"], out var siteId) ? siteId : 1;

    public async Task<ControlGroup> GetGroupAsync(int contestId, int siteId)
    {
        var contest = await _contestRepository.GetContestByIdAsync(contestId, siteId)
            ?? throw new KeyNotFoundException("Concurso no encontrado.");
        if (!contest.IsExam)
        {
            throw new InvalidOperationException("El concurso no está marcado como examen.");
        }

        return await EnsureGroupAsync(contest);
    }

    public async Task<LabLoginResult> LoginAsync(string username, string password, string clientIp)
    {
        PublicAuthenticatedUser user;
        try
        {
            user = await _publicService.LoginAsync(username, password, SiteId, clientIp);
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException)
        {
            return new LabLoginResult { Message = "Usuario o contraseña incorrectos." };
        }

        var contest = await _contestRepository.GetActiveExamForUserAsync(user.UserId, SiteId, DateTime.Now);
        if (contest == null)
        {
            return new LabLoginResult { Message = "No tienes un examen activo en este momento." };
        }

        return new LabLoginResult
        {
            Ok = true,
            UserId = user.UserId,
            DisplayName = string.IsNullOrWhiteSpace(user.Nick) ? user.UserId : user.Nick,
            ContestId = contest.ContestId,
            Group = await EnsureGroupAsync(contest),
        };
    }

    private async Task<ControlGroup> EnsureGroupAsync(Contest contest)
    {
        var groupId = $"contest-{contest.ContestId}";
        var label = string.IsNullOrWhiteSpace(contest.Title) ? groupId : contest.Title.Trim();
        var group = new ControlGroup(groupId, label, Token("enroll", groupId), Token("admin", groupId));
        await _groupStore.EnsureAsync(group);
        return group;
    }

    private string Token(string purpose, string groupId)
    {
        var secret = _configuration["ControlServer:TokenSecret"];
        if (string.IsNullOrEmpty(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException("ControlServer:TokenSecret must be configured with at least 32 characters.");
        }

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{purpose}:{groupId}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
