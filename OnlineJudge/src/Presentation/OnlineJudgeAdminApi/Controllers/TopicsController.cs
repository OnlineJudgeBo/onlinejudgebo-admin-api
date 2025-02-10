using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleManager.Core.Domain.Abstractions.Services;
using ScheduleManager.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/[controller]")]
[Authorize]
public class TopicsController : ControllerBase
{
    private readonly ITopicService _topicService;
    private readonly IMapper _mapper;

    public TopicsController(ITopicService topicService, IMapper mapper)
    {
        _topicService = topicService ?? throw new ArgumentNullException(nameof(topicService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    [HttpGet()]
    public async Task<IActionResult> GetAllTopicsAsync()
    {
        return Ok(await _topicService.GetAllTopicsAsync());
    }

    [HttpPost()]
    public async Task<IActionResult> AddTopicAsync(TopicForCreating topicForCreating)
    {
        Topic newTopic = _mapper.Map<Topic>(topicForCreating);

        await _topicService.AddTopicAsync(newTopic);
        return Ok(await _topicService.GetAllTopicsAsync());
    }

    [HttpPost("{id:int}/classification")]
    public async Task<IActionResult> AddClassificationToTopic(int id, TopicAddClassificationForCreation addClassification)
    {
        Topic newTopic = _mapper.Map<Topic>(addClassification);
        await _topicService.AddClassificationToTopic(id, newTopic);
        return Created();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateClassificationFromTopic(ClassificationForUpdate topicForCreating, int id)
    {
        Classification classification = _mapper.Map<Classification>(topicForCreating);
        await _topicService.UpdateClassification(classification, id);
        return NoContent();
    }
}
