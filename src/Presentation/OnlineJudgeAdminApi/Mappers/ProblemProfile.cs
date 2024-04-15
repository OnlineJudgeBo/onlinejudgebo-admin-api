using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
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

    }
}
