using AutoMapper;
using ScheduleManager.Core.Domain.Models;
using ScheduleManager.Infrastructure.Database.Models;

namespace ScheduleManager.Infrastructure.Database.Mappers
{
    public class DbTeacherProfile : Profile
    {
        public DbTeacherProfile()
        {
            CreateMap<DbTeacher, Teacher>()
                .ForMember(dest => dest.Schedules, opt => opt.MapFrom(src =>
                    src.Schedules.Select(s => new Schedule
                    {
                        Id = s.Id,
                        DayOfWeek = s.DayOfWeek,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Subject = s.Subject != null ? new Subject { Id = s.Subject.Id, Name = s.Subject.Name } : null
                    }).ToList()
                ));
        }
    }
}
