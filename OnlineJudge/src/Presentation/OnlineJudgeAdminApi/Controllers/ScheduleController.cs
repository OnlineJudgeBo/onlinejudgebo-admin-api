using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdminApi.Helpers;
using OnlineJudgeAdmin.Core.Domain.Models;
using Microsoft.AspNetCore.Authorization;

namespace OnlineJudgeAdminApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize]

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
        public async Task<IActionResult> GetSchedule()
        {
            var schedules = await _scheduleService.GetSchedulesAsync();
            return Ok(schedules);
        }
    }
}
