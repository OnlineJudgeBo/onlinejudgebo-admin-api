using AutoMapper;
using ScheduleManager.Core.Domain.Models;
using ScheduleManager.Infrastructure.Database.Models;

namespace ScheduleManager.Infrastructure.Database.Mappers;

public class DbSchedulesProfile : Profile
{
    public DbSchedulesProfile()
    {
        CreateMap<DbSchedule, Schedule>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.DayOfWeek, opt => opt.MapFrom(src => src.DayOfWeek))
            .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.StartTime))
            .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.EndTime))
            .ForMember(dest => dest.Subject, opt => opt.MapFrom(src => new Subject
            {
                Id = src.Subject.Id,
                Name = src.Subject.Name
            }))
            .ForMember(dest => dest.Teacher, opt => opt.MapFrom(src => src.Teacher != null ? new Teacher
            {
                Id = src.Teacher.Id,
                Name = src.Teacher.Name
            } : null))
            .ForMember(dest => dest.Assistant, opt => opt.MapFrom(src => src.Assistant != null ? new Teacher
            {
                Id = src.Assistant.Id,
                Name = src.Assistant.Name
            } : null))
            .ReverseMap();
    }
}
