using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdminApi.Helpers;
using OnlineJudgeAdmin.Core.Domain.Models;
using ScheduleManager.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.DataTransferObjects;
using Microsoft.AspNetCore.Authorization;

namespace OnlineJudgeAdminApi.Controllers
{
    [Route("api/schedule-management/teachers")]
    [ApiController]
    [Authorize]

    public class TeachersController : ControllerBase
    {
        private readonly IScheduleService _scheduleService;
        private readonly UserClaimsHelper _userClaimsHelper;
        private readonly IMapper _mapper;
        private readonly CurrentUser _currentUser;

        public TeachersController(IScheduleService scheduleService, UserClaimsHelper userClaimsHelper, IMapper mapper)
        {
            _scheduleService = scheduleService ?? throw new ArgumentNullException(nameof(scheduleService));
            _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _currentUser = _userClaimsHelper.GetUserContextRole();
        }

        [HttpPost]
        [HttpPost("teacher")]
        public async Task<IActionResult> CreateScheduleTeacher([FromBody] TeacherScheduleForCreation teacherScheduleDto)
        {
            var schedule = await _scheduleService.CreateTeacherAsync(teacherScheduleDto.TeacherName);

            if (schedule == null)
                return BadRequest($"No se pudo crear el horario para el profesor {teacherScheduleDto.TeacherName}.");

            return Ok(schedule);
        }

        [HttpPut("{id}")]
        [HttpPut("teacher/{id}")]
        public async Task<IActionResult> UpdateTeacher(int id, [FromBody] TeacherScheduleForCreation teacherScheduleDto)
        {
            return Ok(await _scheduleService.UpdateTeacherAsync(id, teacherScheduleDto.TeacherName));
        }

        [HttpDelete("{id}")]
        [HttpDelete("teacher/{id}")]
        public async Task<IActionResult> DeleteTeacher(int id)
        {
            await _scheduleService.DeleteTeacherAsync(id);
            return Ok();
        }

        [HttpGet]
        [HttpGet("teachers")]
        public async Task<IActionResult> GetTeachers()
        {
            var teachers = await _scheduleService.GetTeachersAsync();
            return Ok(teachers);
        }
    }
}
