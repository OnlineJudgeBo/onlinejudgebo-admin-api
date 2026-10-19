namespace OnlineJudgeAdmin.Core.Domain.Models;

// Values of the `defunct` column, shared by problems and contests.
public static class DefunctStates
{
    public const string Visible = "N";
    public const string Hidden = "Y";   // not listed, still opens by link (or inside its contest)
    public const string Official = "O"; // official / practice archive
    public const string Deleted = "D";  // soft delete, contests only
}
