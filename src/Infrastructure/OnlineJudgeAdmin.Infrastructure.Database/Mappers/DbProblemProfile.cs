using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Mappers
{
    public class DbProblemProfile : Profile
    {
        public DbProblemProfile()
        {
            CreateMap<DbRole, Role>();
            CreateMap<DbUser, User>();

            CreateMap<DbContest, Contest>();

            CreateMap<DbUserSetting, UserSetting>();
            CreateMap<DbUserActivity, UserActivity>();

            CreateMap<DbCompileinfo, Compileinfo>();
            CreateMap<DbContestProblem, ContestProblem>();
            CreateMap<ContestProblem, DbContestProblem>();

            CreateMap<DbLoginlog, Loginlog>();
            CreateMap<DbNews, News>();

            CreateMap<DbProblem, Problem>();
            CreateMap<Problem, DbProblem>();

            CreateMap<DbRuntimeinfo, Runtimeinfo>();
            CreateMap<DbSolution, Solution>();
            CreateMap<DbSourceCode, SourceCode>();

            CreateMap<DbUserProfile, UserProfile>();

            CreateMap<DbTopic, Topic>();
            CreateMap<Topic, DbTopic>();

            CreateMap<DbClassification, Classification>();
            CreateMap<Classification, DbClassification>();

            CreateMap<UserRole, DbUserRole>();
            CreateMap<DbUserRole, UserRole>();
        }
    }
}
