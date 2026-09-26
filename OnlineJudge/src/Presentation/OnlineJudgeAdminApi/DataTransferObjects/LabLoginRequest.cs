namespace OnlineJudgeAdminApi.DataTransferObjects;

// Body the lab ISO login screen sends (contestants-login-build-payload.py).
public class LabLoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? MachineId { get; set; }
}
