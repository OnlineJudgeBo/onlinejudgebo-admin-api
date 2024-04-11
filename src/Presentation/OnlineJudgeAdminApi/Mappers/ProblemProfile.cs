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
        .ForMember(dest => dest.ContestProblems, opt => opt.MapFrom(src => src.SelectedProblem))
        .ForMember(dest => dest.ContestUsers, opt => opt.MapFrom(src => src.SelectedUser));

        CreateMap<ClassificationsForCreation, Classification>();
        CreateMap<UserAvailableForRequest, UserProfile>();
    }
}
