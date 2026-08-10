using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Controllers;

[Route("/api/schedule-management/teachers")]
[ApiController]
[Authorize]
public class TeachersController : ControllerBase
{
    private readonly IScheduleService _scheduleService;

    public TeachersController(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService ?? throw new ArgumentNullException(nameof(scheduleService));
    }

    [HttpPost]
    public async Task<IActionResult> CreateTeacher([FromBody] TeacherScheduleForCreation teacherScheduleDto)
    {
        var teacher = await _scheduleService.CreateTeacherAsync(teacherScheduleDto.TeacherName);
        return Ok(teacher);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTeacher(int id, [FromBody] TeacherScheduleForCreation teacherScheduleDto)
    {
        return Ok(await _scheduleService.UpdateTeacherAsync(id, teacherScheduleDto.TeacherName));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTeacher(int id)
    {
        await _scheduleService.DeleteTeacherAsync(id);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetTeachers()
    {
        var teachers = await _scheduleService.GetTeachersAsync();
        return Ok(teachers);
    }
}
