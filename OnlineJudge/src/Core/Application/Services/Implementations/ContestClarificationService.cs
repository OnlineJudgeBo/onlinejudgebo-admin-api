using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

// Clarifications: participants ask before or during the contest, never after; staff answer
// the asker only or everyone, and post announcements.
public class ContestClarificationService : IContestClarificationService
{
    private static readonly TimeSpan QuestionInterval = TimeSpan.FromSeconds(30);

    private readonly IContestClarificationRepository _clarificationRepository;
    private readonly IContestsRepository _contestRepository;
    private readonly IPublicRepository _publicRepository;

    public ContestClarificationService(IContestClarificationRepository clarificationRepository, IContestsRepository contestRepository, IPublicRepository publicRepository)
    {
        _clarificationRepository = clarificationRepository ?? throw new ArgumentNullException(nameof(clarificationRepository));
        _contestRepository = contestRepository ?? throw new ArgumentNullException(nameof(contestRepository));
        _publicRepository = publicRepository ?? throw new ArgumentNullException(nameof(publicRepository));
    }

    public async Task<IReadOnlyList<ContestClarification>> GetAllAsync(int contestId, int siteId)
    {
        await GetContestAsync(contestId, siteId);
        return await _clarificationRepository.GetAsync(contestId, siteId, null);
    }

    public Task<IReadOnlyList<ContestClarificationPendingCount>> GetPendingCountsAsync(int siteId)
    {
        return _clarificationRepository.GetPendingCountsAsync(siteId);
    }

    public Task<ContestClarification> AnswerAsync(int contestId, int id, string answer, bool isPublic, CurrentUser currentUser)
    {
        return _clarificationRepository.AnswerAsync(contestId, currentUser.SiteId, id, CleanText(answer, "La respuesta", ContestClarification.MaxAnswerLength), isPublic, currentUser.UserId, DateTime.Now);
    }

    public async Task<ContestClarification> AnnounceAsync(int contestId, int? problemNum, string text, CurrentUser currentUser)
    {
        var contest = await GetContestAsync(contestId, currentUser.SiteId);
        CheckProblemNum(contest, problemNum);
        var now = DateTime.Now;
        return await _clarificationRepository.AddAsync(new ContestClarification
        {
            ContestId = contestId,
            ProblemNum = problemNum,
            Answer = CleanText(text, "El anuncio", ContestClarification.MaxAnswerLength),
            IsPublic = true,
            AnsweredBy = currentUser.UserId,
            CreatedAt = now,
            AnsweredAt = now
        }, currentUser.SiteId);
    }

    public async Task<IReadOnlyList<ContestClarification>> GetForParticipantAsync(int siteId, int contestId, CurrentUser? currentUser)
    {
        await _publicRepository.CheckContestAccessAsync(siteId, contestId, currentUser);
        var userId = currentUser?.UserId ?? string.Empty;
        var items = await _clarificationRepository.GetAsync(contestId, siteId, userId);
        foreach (var item in items)
        {
            // Public answers to someone else's question don't say who asked; nobody sees the judge's user.
            if (item.UserId != userId)
            {
                item.UserId = null;
            }
            item.AnsweredBy = null;
        }
        return items;
    }

    public async Task<ContestClarification> AskAsync(int siteId, int contestId, int? problemNum, string question, CurrentUser currentUser)
    {
        await _publicRepository.CheckContestAccessAsync(siteId, contestId, currentUser);
        var contest = await GetContestAsync(contestId, siteId);
        var now = DateTime.Now;
        if (contest.EndTime < now)
        {
            throw new InvalidOperationException("El concurso ya terminó; no se reciben más preguntas.");
        }
        CheckProblemNum(contest, problemNum);
        var text = CleanText(question, "La pregunta", ContestClarification.MaxQuestionLength);

        var last = await _clarificationRepository.GetLastQuestionAtAsync(contestId, siteId, currentUser.UserId);
        if (last.HasValue && now - last.Value < QuestionInterval)
        {
            throw new InvalidOperationException("Espera unos segundos antes de enviar otra pregunta.");
        }

        return await _clarificationRepository.AddAsync(new ContestClarification
        {
            ContestId = contestId,
            UserId = currentUser.UserId,
            ProblemNum = problemNum,
            Question = text,
            CreatedAt = now
        }, siteId);
    }

    private async Task<Contest> GetContestAsync(int contestId, int siteId)
    {
        return await _contestRepository.GetContestByIdAsync(contestId, siteId)
            ?? throw new KeyNotFoundException("No se encontró el concurso con ID: " + contestId);
    }

    private static void CheckProblemNum(Contest contest, int? problemNum)
    {
        if (problemNum.HasValue && contest.ContestProblems.All(problem => problem.Num != problemNum.Value))
        {
            throw new ArgumentException("El problema no pertenece al concurso.");
        }
    }

    private static string CleanText(string? text, string what, int maxLength)
    {
        text = text?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Length > maxLength)
        {
            throw new ArgumentException($"{what} debe tener entre 1 y {maxLength} caracteres.");
        }
        return text;
    }
}
