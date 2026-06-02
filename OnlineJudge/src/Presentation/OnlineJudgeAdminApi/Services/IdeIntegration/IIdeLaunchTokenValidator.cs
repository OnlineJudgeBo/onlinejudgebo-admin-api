namespace OnlineJudgeAdminApi.Services.IdeIntegration;

public interface IIdeLaunchTokenValidator
{
    IdeLaunchClaims Validate(string? token);
}
