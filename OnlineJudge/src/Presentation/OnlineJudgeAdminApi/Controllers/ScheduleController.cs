using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;
using ScheduleManager.Core.Domain.Abstractions.Services;
using ScheduleManager.Core.Domain.Models;

namespace OnlineJudgeAdminApi.Controllers
{
    [Route("api/schedule-management/schedules")]
    [ApiController]
    [Authorize]

    public class ScheduleController : ControllerBase
    {
        private readonly IScheduleService _scheduleService;
        private readonly UserClaimsHelper _userClaimsHelper;
        private readonly IMapper _mapper;
        private readonly CurrentUser _currentUser;

        public ScheduleController(IScheduleService scheduleService, UserClaimsHelper userClaimsHelper, IMapper mapper)
        {
            _scheduleService = scheduleService ?? throw new ArgumentNullException(nameof(scheduleService));
            _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _currentUser = _userClaimsHelper.GetUserContextRole();
        }

        [HttpGet]
        public async Task<IActionResult> GetSchedules()
        {
            var schedules = await _scheduleService.GetSchedulesAsync();
            return Ok(schedules);
        }

        [HttpGet("teachers")]
        public async Task<IActionResult> GetSchedulesWithTeachers()
        {
            var schedules = await _scheduleService.GetSchedulesWithTeachersAsync();
            return Ok(schedules);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            await _scheduleService.DeleteScheduleAsync(id);
            return Ok();
        }

        [HttpPost()]
        public async Task<IActionResult> CreateSchedule(ScheduleForCreation scheduleForCreation)
        {
            ScheduleForCreationModel schedule = _mapper.Map<ScheduleForCreationModel>(scheduleForCreation);
            await _scheduleService.CreateScheduleAsync(schedule);
            return Ok("Horarios creados correctamente.");
        }
    }
}
