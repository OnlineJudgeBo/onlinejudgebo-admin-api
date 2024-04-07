using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Mappers;

public class ProblemProfile : Profile
{
    public ProblemProfile()
    {
        CreateMap<ProblemForCreation, Problem>();
        CreateMap<ProblemForUpdate, Problem>();

        CreateMap<ClassificationsForCreation, Classification>();
        CreateMap<UserAvailableForRequest, UserProfile>();
    }
}
