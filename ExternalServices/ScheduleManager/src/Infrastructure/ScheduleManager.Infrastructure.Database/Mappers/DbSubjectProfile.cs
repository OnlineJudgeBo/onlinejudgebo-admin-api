using AutoMapper;
using ScheduleManager.Core.Domain.Models;
using ScheduleManager.Infrastructure.Database.Models;

namespace ScheduleManager.Infrastructure.Database.Mappers;

public class DbSubjectProfile : Profile
{
    public DbSubjectProfile()
    {
        CreateMap<DbSubject, Subject>()
        .ReverseMap();
    }
}
