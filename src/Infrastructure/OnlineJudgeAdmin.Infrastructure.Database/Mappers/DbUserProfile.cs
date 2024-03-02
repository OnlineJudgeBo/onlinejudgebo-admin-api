using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

namespace OnlineJudgeAdmin.Infrastructure.Database.Mappers
{
    public class DbUserProfile : Profile
    {
        public DbUserProfile()
        {
            /*CreateMap<User, DbUser>()
                .ForMember(dest => dest._Id, opt => opt.MapFrom(src => src.Id));

            CreateMap<DbUser, User>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._Id));
            */
        }
    }
}
