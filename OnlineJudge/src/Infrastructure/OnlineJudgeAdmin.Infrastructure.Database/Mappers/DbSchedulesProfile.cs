using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Mappers;

public class DbSchedulesProfile : Profile
{
    public DbSchedulesProfile()
    {
        CreateMap<DbSchedule, Schedule>()
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
            .ReverseMap();
    }
}
