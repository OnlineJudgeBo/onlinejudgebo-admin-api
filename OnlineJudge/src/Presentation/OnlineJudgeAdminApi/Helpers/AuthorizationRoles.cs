using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdminApi.Helpers;

public static class AuthorizationRoles
{
    public const string Administrador = nameof(UserRolesEnum.Administrador);
    public const string Docente = nameof(UserRolesEnum.Docente);
    public const string Auxiliar = nameof(UserRolesEnum.Auxiliar);

    public const string AdministradorDocenteAuxiliar = Administrador + "," + Docente + "," + Auxiliar;
}
