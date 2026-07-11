using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Mappers;

public class DbTeacherProfile : Profile
{
    public DbTeacherProfile()
    {
        CreateMap<DbTeacher, Teacher>()
            .ForMember(dest => dest.Schedules, opt => opt.MapFrom(src =>
                src.Schedules.Select(schedule => new Schedule
                {
                    Id = schedule.Id,
                    DayOfWeek = schedule.DayOfWeek,
                    StartTime = schedule.StartTime,
                    EndTime = schedule.EndTime,
                    Subject = new Subject
                    {
                        Id = schedule.Subject.Id,
                        Name = schedule.Subject.Name
                    }
                }).ToList()
            ));
    }
}
