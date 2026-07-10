using AutoMapper;
using OnlineJudgeAdminApi.DataTransferObjects;
using ScheduleManager.Core.Domain.Models;

namespace OnlineJudgeAdminApi.Mappers;

public class ScheduleCreationProfile : Profile
{
    public ScheduleCreationProfile()
    {
        CreateMap<ScheduleForCreation, ScheduleForCreationModel>()
        .ForMember(dest => dest.ScheduleDays, opt => opt.MapFrom(src => src.ScheduleDays))
        .ForMember(dest => dest.ScheduleTime, opt => opt.MapFrom(src => src.ScheduleTime))
        .ForMember(dest => dest.Subject, opt => opt.MapFrom(src => src.Subject))
        .ForMember(dest => dest.Teacher, opt => opt.MapFrom(src => src.Teacher))
        .ForMember(dest => dest.Assistant, opt => opt.MapFrom(src => src.Assistant));
    }
}
