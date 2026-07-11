using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Mappers;

public class DbSubjectProfile : Profile
{
    public DbSubjectProfile()
    {
        CreateMap<DbSubject, Subject>()
        .ReverseMap();
    }
}
