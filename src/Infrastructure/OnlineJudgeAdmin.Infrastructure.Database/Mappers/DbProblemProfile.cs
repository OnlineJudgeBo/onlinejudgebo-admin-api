using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Mappers
{
    public class DbProblemProfile : Profile
    {
        public DbProblemProfile()
        {
            CreateMap<DbContestProblem, ContestProblem>();
            CreateMap<ContestProblem, DbContestProblem>();
            CreateMap<DbContestUser, ContestUser>();
            CreateMap<DbContest, Contest>()
            .ReverseMap()
            .ForMember(dest => dest.ContestId, opt => opt.MapFrom(src => src.ContestId))
            .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Title))
            .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.StartTime))
            .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.EndTime))
            .ForMember(dest => dest.Defunct, opt => opt.MapFrom(src => src.Defunct))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.Private, opt => opt.MapFrom(src => src.Private))
            .ForMember(dest => dest.Langmask, opt => opt.MapFrom(src => src.Langmask))
            .ForMember(dest => dest.ProgrammingLanguages, opt => opt.MapFrom(src => src.ProgrammingLanguages))
            .ForMember(dest => dest.Obi, opt => opt.MapFrom(src => src.Obi))
            .ForMember(dest => dest.ContestProblems, opt => opt.MapFrom(src => src.ContestProblems))
            .ForMember(dest => dest.ContestUsers, opt => opt.MapFrom(src => src.ContestUsers));

            CreateMap<DbProgrammingLanguage, ProgrammingLanguage>();
            CreateMap<ProgrammingLanguage, DbProgrammingLanguage>();

            CreateMap<DbContestUser, ContestUser>();
            CreateMap<ContestUser, DbContestUser>();

            CreateMap<DbUser, User>();
            CreateMap<User, DbUser>();

            CreateMap<Contest, DbContest>();

            //CreateMap<User, DbContestUser>();

            CreateMap<DbUserSetting, UserSetting>();
            CreateMap<DbUserActivity, UserActivity>();
            CreateMap<DbUserProfile, UserProfile>();

            CreateMap<DbCompileinfo, Compileinfo>();


            CreateMap<DbLoginlog, Loginlog>();
            CreateMap<DbNews, News>();

            CreateMap<DbProblem, Problem>();
            CreateMap<DbUpdateProblem, Problem>();
            CreateMap<Problem, DbUpdateProblem>();


            CreateMap<Problem, DbProblem>();

            CreateMap<DbRuntimeinfo, Runtimeinfo>();
            CreateMap<DbSolution, Solution>();
            CreateMap<DbSourceCode, SourceCode>();


            CreateMap<DbTopic, Topic>();
            CreateMap<Topic, DbTopic>();

            CreateMap<DbClassification, Classification>();
            CreateMap<Classification, DbClassification>();

            CreateMap<DbRole, Role>();
            CreateMap<Role, DbRole>();

            CreateMap<Privilege, DbPrivilege>();

            CreateMap<UserRole, DbUserRole>()
                .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => src.RoleId))
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId));

            CreateMap<DbUserRole, UserRole>()
                .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => src.RoleId))
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId));
        }
    }
}
