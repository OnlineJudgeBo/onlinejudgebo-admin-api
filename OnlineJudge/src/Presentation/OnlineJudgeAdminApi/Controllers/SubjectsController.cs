using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Controllers;

[Route("/api/schedule-management/subjects")]
[ApiController]
[Authorize]
public class SubjectsController : ControllerBase
{
    private readonly IScheduleService _scheduleService;

    public SubjectsController(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService ?? throw new ArgumentNullException(nameof(scheduleService));
    }

    [HttpPost]
    public async Task<IActionResult> CreateSubject([FromBody] SubjectForCreation subject)
    {
        var createdSubject = await _scheduleService.CreateSubjectAsync(new Subject { Name = subject.SubjectName });
        return Ok(createdSubject);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSubject(int id, [FromBody] SubjectForCreation subject)
    {
        return Ok(await _scheduleService.UpdateSubjectAsync(id, new Subject { Name = subject.SubjectName }));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSubject(int id)
    {
        await _scheduleService.DeleteSubjectAsync(id);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetSubjects()
    {
        var subjects = await _scheduleService.GetSubjectsAsync();
        return Ok(subjects);
    }
}
