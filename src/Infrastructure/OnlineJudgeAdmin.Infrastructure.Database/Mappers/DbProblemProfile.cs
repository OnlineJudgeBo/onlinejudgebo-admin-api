using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

namespace OnlineJudgeAdmin.Infrastructure.Database.Mappers
{
    public class DbProblemProfile : Profile
    {
        public DbProblemProfile()
        {
            CreateMap<DbProblem, Problem>();            
        }
    }
}
