using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

namespace OnlineJudgeAdmin.Infrastructure.Database.Mappers
{
    public class DbProblemProfile : Profile
    {
        public DbProblemProfile()
        {
            CreateMap<DbTag, Tag>();

            CreateMap<DbProblem, Problem>()
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags));
        }
    }
}
