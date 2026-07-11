using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Controllers;

[Route("api/schedule-management/subjects/{subjectId}/assistant")]
[ApiController]
[Authorize]
public class SubjectAssistantsController : ControllerBase
{
    private readonly IScheduleService _scheduleService;

    public SubjectAssistantsController(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService ?? throw new ArgumentNullException(nameof(scheduleService));
    }

    [HttpGet]
    public async Task<IActionResult> GetSubjectAssistant(int subjectId)
    {
        var assistant = await _scheduleService.GetSubjectAssistantBySubjectIdAsync(subjectId);
        return Ok(assistant);
    }

    [HttpPut]
    public async Task<IActionResult> UpsertSubjectAssistant(int subjectId, [FromBody] SubjectAssistantForCreation assistantForCreation)
    {
        var assistant = await _scheduleService.UpsertSubjectAssistantAsync(subjectId, assistantForCreation.Name, assistantForCreation.Schedule);
        return Ok(assistant);
    }
}
