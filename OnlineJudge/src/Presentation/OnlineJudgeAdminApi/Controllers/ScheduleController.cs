using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Controllers;

[Route("/api/schedule-management/schedules")]
[ApiController]
[Authorize]
public class ScheduleController : ControllerBase
{
    private readonly IScheduleService _scheduleService;
    private readonly IMapper _mapper;

    public ScheduleController(IScheduleService scheduleService, IMapper mapper)
    {
        _scheduleService = scheduleService ?? throw new ArgumentNullException(nameof(scheduleService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
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

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSchedule(int id, ScheduleForCreation scheduleForUpdate)
    {
        ScheduleForCreationModel schedule = _mapper.Map<ScheduleForCreationModel>(scheduleForUpdate);
        return Ok(await _scheduleService.UpdateScheduleAsync(id, schedule));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        await _scheduleService.DeleteScheduleAsync(id);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> CreateSchedule(ScheduleForCreation scheduleForCreation)
    {
        ScheduleForCreationModel schedule = _mapper.Map<ScheduleForCreationModel>(scheduleForCreation);
        await _scheduleService.CreateScheduleAsync(schedule);
        return Ok("Horarios creados correctamente.");
    }
}
