using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;
using ScheduleManager.Core.Domain.Abstractions.Services;

namespace OnlineJudgeAdminApi.Controllers
{
    [Route("api/schedule-management/subjects/{subjectId}/assistant")]
    [ApiController]
    [Authorize]
    public class AssistantsController : ControllerBase
    {
        private readonly IScheduleService _scheduleService;
        private readonly UserClaimsHelper _userClaimsHelper;
        private readonly CurrentUser _currentUser;

        public AssistantsController(IScheduleService scheduleService, UserClaimsHelper userClaimsHelper)
        {
            _scheduleService = scheduleService ?? throw new ArgumentNullException(nameof(scheduleService));
            _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
            _currentUser = _userClaimsHelper.GetUserContextRole();
        }

        [HttpGet]
        public async Task<IActionResult> GetAssistant(int subjectId)
        {
            var assistant = await _scheduleService.GetAssistantBySubjectIdAsync(subjectId);
            return Ok(assistant);
        }

        [HttpPut]
        public async Task<IActionResult> UpsertAssistant(int subjectId, [FromBody] AssistantForCreation assistantForCreation)
        {
            var assistant = await _scheduleService.UpsertAssistantAsync(subjectId, assistantForCreation.Name, assistantForCreation.Schedule);
            return Ok(assistant);
        }
    }
}
