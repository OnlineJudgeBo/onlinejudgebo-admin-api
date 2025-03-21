using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdminApi.Helpers;
using OnlineJudgeAdmin.Core.Domain.Models;
using ScheduleManager.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.DataTransferObjects;
using ScheduleManager.Core.Domain.Models;
using Microsoft.AspNetCore.Authorization;

namespace OnlineJudgeAdminApi.Controllers
{
    [Route("api/schedule-management/subjects")]
    [ApiController]
    [Authorize]

    public class SubjectsController : ControllerBase
    {
        private readonly IScheduleService _scheduleService;
        private readonly UserClaimsHelper _userClaimsHelper;
        private readonly IMapper _mapper;
        private readonly CurrentUser _currentUser;

        public SubjectsController(IScheduleService scheduleService, UserClaimsHelper userClaimsHelper, IMapper mapper)
        {
            _scheduleService = scheduleService ?? throw new ArgumentNullException(nameof(scheduleService));
            _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _currentUser = _userClaimsHelper.GetUserContextRole();
        }

        [HttpPost()]
        public async Task<IActionResult> CreateScheduleTeacher([FromBody] SubjectForCreation subject)
        {
            var schedule = await _scheduleService.CreateSubjectAsync(new Subject { Name = subject.SubjectName });

            if (schedule == null)
                return BadRequest($"No se pudo crear el horario para el profesor {subject.SubjectName}.");

            return Ok(schedule);
        }

        [HttpGet()]
        public async Task<IActionResult> GetSubjects()
        {
            var teachers = await _scheduleService.GetSubjectAsync();
            return Ok(teachers);
        }
    }
}
