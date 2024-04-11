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
        CreateMap<ContestForCreation, Contest>()
        .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.StartDate))
        .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.EndDate))
        .ForMember(dest => dest.ContestProblems, opt => opt.MapFrom(src => src.SelectedProblem))
        .ForMember(dest => dest.ContestUsers, opt => opt.MapFrom(src => src.SelectedUser))
        .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Title))
        .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description));

        CreateMap<ClassificationsForCreation, Classification>();
        CreateMap<UserAvailableForRequest, UserProfile>();
    }
}
