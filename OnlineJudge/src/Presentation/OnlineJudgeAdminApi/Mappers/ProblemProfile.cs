using AutoMapper;
using ScheduleManager.Core.Domain.Models;
using ScheduleManager.Infrastructure.Database.Models;
using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Mappers;

public class ProblemProfile : Profile
{
    public ProblemProfile()
    {
        CreateMap<ProblemForCreation, Problem>();
        CreateMap<ProblemForUpdate, Problem>();
        CreateMap<ProblemForContestCreation, ContestProblem>();
        CreateMap<DbContestProblem, Problem>();
        CreateMap<UserForContestCreation, ContestUser>();
        CreateMap<ClassificationsForCreation, Classification>();
        CreateMap<UserAvailableForRequest, UserProfile>();
        CreateMap<RemoteExecutionForCreation, RemoteExecutionRequest>();

        CreateMap<ContestForCreation, Contest>()
        .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.StartDate))
        .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.EndDate))
        .ForMember(dest => dest.ContestProblems, opt => opt.MapFrom(src => src.SelectedProblem))
        .ForMember(dest => dest.ContestUsers, opt => opt.MapFrom(src => src.SelectedUser))
        .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Title))
        .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
        .ForMember(dest => dest.Private, opt => opt.MapFrom(src => src.IsPrivate))
        .ForMember(dest => dest.ProgrammingLanguages, opt => opt.MapFrom(src => src.selectedLanguages));

        CreateMap<ContestForUpdate, Contest>()
        .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.StartDate))
        .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.EndDate))
        .ForMember(dest => dest.ContestProblems, opt => opt.MapFrom(src => src.SelectedProblem))
        .ForMember(dest => dest.ContestUsers, opt => opt.MapFrom(src => src.SelectedUser))
        .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Title))
        .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
        .ForMember(dest => dest.Private, opt => opt.MapFrom(src => src.IsPrivate))
        .ForMember(dest => dest.ProgrammingLanguages, opt => opt.MapFrom(src => src.selectedLanguages));

        CreateMap<ProgrammingLanguageForContestCreation, ProgrammingLanguage>()
            .ForMember(dest => dest.LanguageId, opt => opt.MapFrom(src => src.LanguageId));

        CreateMap<UserForUpdate, User>()
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserName))
            .ForMember(dest => dest.UserProfile, opt => opt.MapFrom(src => new UserProfile
            {
                UserId = src.UserName,
                Email = src.Email,
                Nick = src.Name,
                Lastname = src.LastName
            }));

        CreateMap<RemoteExecutionResult, Solution>()
            .ForMember(dest => dest.SolutionId, opt => opt.MapFrom(src => src.RemoteId))
            .ForMember(dest => dest.Memory, opt => opt.MapFrom(src => src.Memory))
            .ForMember(dest => dest.InDate, opt => opt.MapFrom(src => src.InDate))
            .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Result))
            .ForMember(dest => dest.JudgeTime, opt => opt.MapFrom(src => src.JudgeTime));

        CreateMap<TopicForCreating, Topic>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name));

        CreateMap<TopicAddClassificationForCreation, Topic>()
            .ForMember(dest => dest.Classifications, opt => opt.MapFrom(src => src.Classifications.Select(c => new Classification
            {
                Name = c.Name.ToString()
            }).ToList()));

        CreateMap<ClassificationAddToTopic, Classification>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.ToString()));

        CreateMap<ClassificationForUpdate, Classification>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name));
    }
}
