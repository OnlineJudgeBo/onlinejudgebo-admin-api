namespace OnlineJudgeAdmin.Core.Domain.Models;

/// <summary>
/// Single source of truth for the values stored in academic.course_user.role.
/// Kept in Spanish to match the rest of the academic domain's user-facing vocabulary
/// (UserRolesEnum, the JWT "roles" claim, and the client UI labels).
/// </summary>
public static class CourseRoleNames
{
    public const string Admin = "administrador";
    public const string Teacher = "docente";
    public const string Assistant = "auxiliar";
    public const string Student = "estudiante";
}
