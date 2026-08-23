using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

public class DbProblemProfile : Profile
{
    public DbProblemProfile()
    {
        CreateMap<DbContestProblem, ContestProblem>();
        CreateMap<ContestProblem, DbContestProblem>();

        CreateMap<DbProblemSample, ProblemSample>();
        CreateMap<ProblemSample, DbProblemSample>();

        CreateMap<DbContestUser, ContestUser>()
        .ReverseMap()
        .ForMember(dest => dest.ContestId, opt => opt.MapFrom(src => src.ContestId))
        .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
        .ForMember(dest => dest.SiteId, opt => opt.MapFrom(src => src.SiteId))
        .ForMember(dest => dest.IsOwner, opt => opt.MapFrom(src => src.IsOwner));

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
        .ForMember(dest => dest.Track, opt => opt.MapFrom(src => src.Track))
        .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level))
        .ForMember(dest => dest.ContestProblems, opt => opt.MapFrom(src => src.ContestProblems))
        .ForMember(dest => dest.ContestUsers, opt => opt.MapFrom(src => src.ContestUsers));

        CreateMap<DbProgrammingLanguage, ProgrammingLanguage>();
        CreateMap<ProgrammingLanguage, DbProgrammingLanguage>();

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

        CreateMap<DbSolution, Solution>()
            .ReverseMap()
            .ForMember(dest => dest.ProblemId, opt => opt.MapFrom(src => src.ProblemId))
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
            .ForMember(dest => dest.Time, opt => opt.MapFrom(src => src.Time))
            .ForMember(dest => dest.Memory, opt => opt.MapFrom(src => src.Memory))
            .ForMember(dest => dest.InDate, opt => opt.MapFrom(src => src.InDate))
            .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Result))
            .ForMember(dest => dest.Language, opt => opt.MapFrom(src => src.Language))
            .ForMember(dest => dest.Num, opt => opt.MapFrom(src => src.Num))
            .ForMember(dest => dest.CodeLength, opt => opt.MapFrom(src => src.CodeLength))
            .ForMember(dest => dest.IsRemoteOj, opt => opt.MapFrom(src => src.IsRemoteOj))
            .ForMember(dest => dest.RemoteId, opt => opt.MapFrom(src => src.RemoteId))
            .ForMember(dest => dest.SiteId, opt => opt.MapFrom(src => src.SiteId));
    }
}
