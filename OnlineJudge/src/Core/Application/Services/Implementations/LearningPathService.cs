using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using static OnlineJudgeAdmin.Core.Application.Services.Helpers.AcademicRequestRules;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class LearningPathService : ILearningPathService
{
    private readonly ILearningPathRepository _repository;

    public LearningPathService(ILearningPathRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IEnumerable<LearningPathTrackSummary>> GetLearningPathsAsync(int siteId, CurrentUser currentUser)
    {
        ValidateSite(siteId, currentUser.SiteId);
        return await _repository.GetLearningPathsAsync(siteId);
    }

    public async Task<LearningPathResponse> GetLearningPathAsync(int siteId, string learningPathKey, CurrentUser currentUser)
    {
        ValidateSite(siteId, currentUser.SiteId);
        if (string.IsNullOrWhiteSpace(learningPathKey))
        {
            throw new ArgumentException("La clave de la ruta de aprendizaje es requerida.");
        }

        return await _repository.GetLearningPathAsync(siteId, learningPathKey);
    }

    public Task<LearningPathResponse> CreateLearningPathAsync(int siteId, CurrentUser currentUser, LearningPathAdminUpsertRequest request)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidateLearningPathRequest(request);
        return _repository.CreateLearningPathAsync(siteId, request);
    }

    public Task<LearningPathResponse> UpdateLearningPathAsync(int siteId, string learningPathKey, CurrentUser currentUser, LearningPathAdminUpsertRequest request)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidateKey(learningPathKey, "Learning path key");
        ValidateLearningPathRequest(request);
        return _repository.UpdateLearningPathAsync(siteId, learningPathKey.Trim(), request);
    }

    public Task DeleteLearningPathAsync(int siteId, string learningPathKey, CurrentUser currentUser)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidateKey(learningPathKey, "Learning path key");
        return _repository.DeleteLearningPathAsync(siteId, learningPathKey.Trim());
    }

    public Task<LearningPathResponse> CreateLearningPathStageAsync(int siteId, string learningPathKey, CurrentUser currentUser, LearningPathStageAdminRequest request)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidateKey(learningPathKey, "Learning path key");
        ValidateKey(request.Key, "Stage key");
        ValidateKey(request.Name, "Stage name");
        ValidatePositiveOrder(request.Order, "Stage order");
        return _repository.CreateLearningPathStageAsync(siteId, learningPathKey.Trim(), request);
    }

    public Task<LearningPathResponse> LinkLearningPathStageAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidateKey(learningPathKey, "Learning path key");
        ValidatePositiveId(stageId, "Stage id");
        return _repository.LinkLearningPathStageAsync(siteId, learningPathKey.Trim(), stageId);
    }

    public Task UnlinkLearningPathStageAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidateKey(learningPathKey, "Learning path key");
        ValidatePositiveId(stageId, "Stage id");
        return _repository.UnlinkLearningPathStageAsync(siteId, learningPathKey.Trim(), stageId);
    }

    public Task<LearningPathResponse> UpdateLearningPathStageAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser, LearningPathStageAdminRequest request)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidatePositiveId(stageId, "Stage id");
        ValidateKey(request.Key, "Stage key");
        ValidateKey(request.Name, "Stage name");
        ValidatePositiveOrder(request.Order, "Stage order");
        return _repository.UpdateLearningPathStageAsync(siteId, learningPathKey.Trim(), stageId, request);
    }

    public Task DeleteLearningPathStageAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidatePositiveId(stageId, "Stage id");
        return _repository.DeleteLearningPathStageAsync(siteId, learningPathKey.Trim(), stageId);
    }

    public Task<LearningPathResponse> CreateLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, CurrentUser currentUser, LearningPathTopicAdminRequest request)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidatePositiveId(stageId, "Stage id");
        ValidateTopicRequest(request);
        return _repository.CreateLearningPathTopicAsync(siteId, learningPathKey.Trim(), stageId, request);
    }

    public Task<LearningPathResponse> UpdateLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, long topicId, CurrentUser currentUser, LearningPathTopicAdminRequest request)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidatePositiveId(stageId, "Stage id");
        ValidatePositiveId(topicId, "Topic id");
        ValidateTopicRequest(request);
        return _repository.UpdateLearningPathTopicAsync(siteId, learningPathKey.Trim(), stageId, topicId, request);
    }

    public Task DeleteLearningPathTopicAsync(int siteId, string learningPathKey, long stageId, long topicId, CurrentUser currentUser)
    {
        ValidateLearningPathManager(siteId, currentUser);
        ValidatePositiveId(stageId, "Stage id");
        ValidatePositiveId(topicId, "Topic id");
        return _repository.DeleteLearningPathTopicAsync(siteId, learningPathKey.Trim(), stageId, topicId);
    }

    public async Task<LearningPathProgressResponse> GetLearningPathProgressAsync(int siteId, string learningPathKey, CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);

        if (string.IsNullOrWhiteSpace(learningPathKey))
        {
            throw new ArgumentException("La clave de la ruta de aprendizaje es requerida.");
        }

        return await _repository.GetLearningPathProgressAsync(siteId, learningPathKey, currentUser.UserId);
    }

    public async Task<LearningPathProgressResponse> SaveLearningPathProgressAsync(
        int siteId,
        string learningPathKey,
        CurrentUser currentUser,
        LearningPathProgressUpdateRequest request)
    {
        ValidateRequestContext(siteId, currentUser);

        if (string.IsNullOrWhiteSpace(learningPathKey))
        {
            throw new ArgumentException("La clave de la ruta de aprendizaje es requerida.");
        }

        return await _repository.SaveLearningPathProgressAsync(
            siteId,
            learningPathKey,
            currentUser.UserId,
            request ?? new LearningPathProgressUpdateRequest());
    }

    private static void ValidateLearningPathManager(int siteId, CurrentUser currentUser)
    {
        ValidateRequestContext(siteId, currentUser);
        if (currentUser.Role != UserRolesEnum.Administrador)
        {
            throw new UnauthorizedAccessException("Only administrators can configure learning paths.");
        }
    }

    private static void ValidateLearningPathRequest(LearningPathAdminUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKey(request.Key, "Learning path key");
        ValidateKey(request.Title, "Learning path title");
        if (request.Version <= 0) throw new ArgumentException("Version debe ser positiva.");
    }

    private static void ValidateTopicRequest(LearningPathTopicAdminRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKey(request.Key, "Topic key");
        ValidateKey(request.Title, "Topic title");
        if (request.ProblemIds.Any(id => id <= 0)) throw new ArgumentException("Los ids de problema deben ser positivos.");
    }

    private static void ValidateKey(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} es requerido.");
    }

    private static void ValidatePositiveId(long id, string name)
    {
        if (id <= 0) throw new ArgumentException($"{name} es requerido.");
    }

    private static void ValidatePositiveOrder(int order, string name)
    {
        if (order <= 0) throw new ArgumentException($"{name} debe ser positivo.");
    }
}
