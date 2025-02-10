using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Schedule.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Mappers
{
    public class DbScheduleProfile : Profile
    {
        public DbScheduleProfile()
        {
            CreateMap<DbSchedule, Schedule>()
                .ForMember(dest => dest.ScheduleId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.DayOfWeek, opt => opt.MapFrom(src => src.DayOfWeek))
                .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.StartTime))
                .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.EndTime))
                .ForMember(dest => dest.Subject, opt => opt.MapFrom(src => src.Subject))
                .ForMember(dest => dest.TeacherId, opt => opt.MapFrom(src => src.TeacherId))
                .ForMember(dest => dest.Teacher, opt => opt.MapFrom(src => src.Teacher))
                .ReverseMap()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ScheduleId))
                .ForMember(dest => dest.DayOfWeek, opt => opt.MapFrom(src => src.DayOfWeek))
                .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.StartTime))
                .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.EndTime))
                .ForMember(dest => dest.Subject, opt => opt.MapFrom(src => src.Subject))
                .ForMember(dest => dest.TeacherId, opt => opt.MapFrom(src => src.TeacherId))
                .ForMember(dest => dest.Teacher, opt => opt.MapFrom(src => src.Teacher));
        }
    }
}
