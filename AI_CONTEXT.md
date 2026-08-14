# Contexto para IA — `onlinejudgebo-admin-api`

> Generado: 2026-08-14. Documento vivo: si cambian rutas, DTOs o contratos, actualizar
> este archivo en el mismo PR. No duplica `INVENTORY.md`/`ARCHITECTURE.md` de la raíz
> del workspace — los complementa con el detalle de endpoints de este repo específico.

## 1. Qué es este proyecto

API administrativa, pública y del IDE de Patito (juezvirtual.com.bo). ASP.NET Core 8,
persistencia en MariaDB vía EF Core. Es el único backend HTTP del workspace: lo
consumen el panel admin (`juezvirtualbo-admin-front`), el portal público
(`juezvirtualbo-client-front`), el IDE web (`patito-ide`) y la suite de contrato
(`juezvirtualbo-api-test`). El kernel de evaluación (`onlinejudge-kernel`) no la
consume por HTTP: comparte la misma base MariaDB y lee/escribe directamente sus tablas.

**Rama activa real: `develop`**, no `main`. `origin/main` está ~170 commits detrás de
`origin/develop` a la fecha de este documento — todo el trabajo reciente (cursos,
roles, learning-paths, instituciones) vive solo en `develop`. Cualquier PR contra este
repo debe apuntar a `develop`, no a `main`.

## 2. Arquitectura en capas

```
Controllers (Presentation, OnlineJudgeAdminApi/Controllers/)
  -> Services y Validators (Application, src/Core/Application/)
      -> Abstractions y Models (Domain, src/Core/Domain/)
          -> EF Core, filesystem, S3, integración IDE (Infrastructure, src/Infrastructure/)
```

- **DTOs de entrada/salida HTTP** viven en `Presentation/OnlineJudgeAdminApi/DataTransferObjects/`
  (sufijo `ForCreation`/`ForUpdate`) y se mapean con AutoMapper a los modelos de
  `Core/Domain/Models/` que usan los services.
- **Autenticación**: JWT emitido por `PublicAuthController`. El claim `site_id` (o
  `siteId`) filtra todos los recursos multi-sitio; `UserClaimsHelper.GetUserContextRole()`
  devuelve un `CurrentUser { UserId, Role, SiteId }` a partir del JWT/contexto HTTP.
- **Roles** (`OnlineJudgeAdmin.Core.Domain.Models.UserRolesEnum`): `Administrador`,
  `Auxiliar`, `Docente`, `Invitado`. Constantes de autorización en
  `Helpers/AuthorizationRoles.cs`: `Administrador`, `Docente`, `Auxiliar`,
  `AdministradorDocenteAuxiliar` (combo usado para permisos académicos).

## 3. Inventario de endpoints

Todas las rutas cuelgan de `/api`. Salvo que se indique lo contrario, requieren
`[Authorize]` (JWT válido); "anónimo" = sin sesión.

### AcademicController — prefijo `/api/academic`

| Verbo | Ruta | Rol | Descripción |
| --- | --- | --- | --- |
| GET | `sites/{siteId}/institutions` | autenticado | Lista instituciones del sitio |
| GET | `sites/{siteId}/institutions/ranking?limit=` | autenticado | Ranking de instituciones |
| GET | `sites/{siteId}/courses/mine` | autenticado | Cursos del usuario actual |
| GET | `sites/{siteId}/courses/manageable` | autenticado | Cursos que el usuario puede administrar |
| POST | `sites/{siteId}/courses` | Admin/Docente/Auxiliar | Crea curso (`AcademicCourseForCreation` → `AcademicCourseCreationRequest`: Name, Description, InstitutionId) |
| POST | `sites/{siteId}/courses/join` | autenticado | Unirse por código de invitación |
| GET | `sites/{siteId}/courses/{courseId}` | autenticado | Detalle de curso |
| GET | `sites/{siteId}/courses/{courseId}/members` | manager del curso | Miembros |
| POST | `sites/{siteId}/courses/{courseId}/members` | manager del curso | Agregar miembro |
| DELETE | `sites/{siteId}/courses/{courseId}/members/{memberUserId}` | manager del curso | Quitar miembro |
| POST | `sites/{siteId}/courses/{courseId}/assignments` | manager del curso | Crear tarea |
| POST | `sites/{siteId}/courses/{courseId}/materials` | manager del curso | Crear material |
| PUT | `sites/{siteId}/courses/{courseId}/materials/{materialId}` | manager del curso | Editar material |
| DELETE | `sites/{siteId}/courses/{courseId}/materials/{materialId}` | manager del curso | Borrar material |
| PUT | `sites/{siteId}/courses/{courseId}/content-order` | manager del curso | Reordenar contenido |
| PUT | `sites/{siteId}/courses/{courseId}/assignments/{assignmentId}` | manager del curso | Editar tarea |
| GET | `sites/{siteId}/courses/{courseId}/assignments/{assignmentId}` | miembro del curso | Detalle de tarea |
| GET | `sites/{siteId}/courses/{courseId}/assignments/{assignmentId}/submissions?page=&pageSize=` | manager del curso | Envíos de una tarea |
| GET | `sites/{siteId}/courses/{courseId}/ranking` | miembro del curso | Ranking del curso |
| GET | `sites/{siteId}/courses/{courseId}/report` / `.../report.csv` | manager del curso | Reporte académico (JSON / CSV) |
| GET | `sites/{siteId}/courses/{courseId}/students/{userId}/progress` | manager del curso | Progreso de un estudiante |
| GET | `sites/{siteId}/learning-paths` | autenticado | Catálogo de rutas de aprendizaje del sitio |
| GET | `sites/{siteId}/learning-paths/{key}` | autenticado | Detalle de una ruta |
| POST/PUT/DELETE | `learning-paths` / `learning-paths/{key}` | Admin/Docente/Auxiliar | CRUD de rutas (sin `siteId` en la URL — global) |
| POST/DELETE | `learning-paths/{key}/stages/{stageId}/link` | Admin/Docente/Auxiliar | Vincular/desvincular etapa existente |
| POST/PUT/DELETE | `learning-paths/{key}/stages(/{stageId})` | Admin/Docente/Auxiliar | CRUD de etapas |
| POST/PUT/DELETE | `learning-paths/{key}/stages/{stageId}/topics(/{topicId})` | Admin/Docente/Auxiliar | CRUD de temas |
| GET/PUT | `sites/{siteId}/learning-paths/{key}/progress` | autenticado | Progreso del usuario en una ruta |

**Nota de contrato incompleto**: `AcademicCourseCreationRequest.InstitutionId` se recibe
en la creación de curso pero **nunca se persiste** — `DbCourse` no tiene columna
`institution_id` (`AcademicRepository.CreateCourseAsync`, líneas ~118-133). Hoy ningún
frontend lo envía, así que no rompe nada, pero es un campo fantasma en el contrato.

### Otros controllers

| Controller | Prefijo | Rutas clave |
| --- | --- | --- |
| `PublicAuthController` | `/api/public/auth` | `POST login`, `POST register`, `POST password-recovery/request`, `POST password-recovery/confirm`, `GET me` — todos anónimos salvo `me` |
| `PublicController` | `/api/public` | Catálogo público: `dashboard`, `problems(+filters/{id}/{id}/statistics)`, `contests(+{id}/problems/{cpId}/{id}/report(.csv))`, `ranking`, `temas`, `languages`, `submissions(+/mine/+{id}/mine/source-codes.zip)`, `activity/*`, `POST submit` (autenticado; body `PublicSubmissionForCreation` con `ProblemId`, `ContestProblemId`, `SourceCode`, `LanguageId`, `ContestId`, `Num`, `CourseId`, `AssignmentId`, `FileName` — delega a lógica académica internamente si `CourseId`/`AssignmentId` están presentes) |
| `SubmissionController` | `/api/Submission` | `GET` (auditoría, filtros page/pageSize/problemId/userId/clientIp), `POST` (envío con contexto académico vía `SubmissionForCreation` — **sin `ContestProblemId`**, DTO más limitado que el de `PublicController`), más rutas absolutas `/api/patito-ide/submissions(+/{id})`, `/api/patito-ide/runs(+/{id})`, `/api/patito-ide/custom-input` para el IDE |
| `IdeContextController` | `/api/patito-ide` | `GET context` — **`[AllowAnonymous]`**, se autentica con el *launch token* firmado (query `token` o Bearer), no con la cookie de sesión normal |
| `IdeLaunchTokenController` | `/api/patito-ide/launch-token` | `POST` — **`[Authorize]`** (sesión normal). Firma server-side el launch token del IDE a partir de `IdeLaunchClaims` derivados de la sesión (`sub`/`site_id` nunca vienen del cliente). **Todavía no está en `develop`** — vive en el PR #8 (`fix/ide-launch-token-server-side`, sin mergear al escribir este documento). Ver `IdeLaunchTokenValidator.cs` (`Validate` + `Issue`, mismo secreto `PATITO_IDE_TOKEN_SECRET`) |
| `ContestsController` | `/api/Contests` | `GET`, `GET {id}`, `POST` (crear), `PUT {id}` (editar), `PUT {id}/promote` |
| `ProblemsController` | `/api/Problems` | `GET(?searchTerm)`, `GET {problem_id}`, `POST` (crear), `PUT {id}`, `PUT {id}/visibility`, `DELETE {id}` |
| `TopicsController` | `/api/Topics` | `GET`, `POST`, `POST {id}/classification`, `PUT {id}` |
| `UsersController` | `/api/Users` | `GET(?searchTerm)`, `POST UsernameIsAvailable`, `POST UserEmailIsAvailable`, `PUT {userId}`, `PUT changePassword/{userId}`, `DELETE {userId}/role/{roleId}`, `DELETE {userId}` |
| `RolesController` | `/api/Roles` (solo Administrador) | `GET` (roles del sitio), `GET rolesAvailable`, `POST {userId}/{role}`, `DELETE {userId}/{role}` |
| `JudgeController` | `/api/Judge` | `GET rejudge/solution/{id}`, `PATCH solution/{id}/verdict`, `GET rejudge/problem/{id}`, `GET rejudge/contest/{id}`, `GET rejudge/range?fromSolutionId=&toSolutionId=`, `GET rejudge/language/{id}`, `GET rejudge/history?limit=`, `POST remoteExecutionAsync`, `POST remoteExecutionResult` — rejudge requiere `AdministradorDocenteAuxiliar` |
| `FileManagerController` | `/api/FileManager` | `POST cloud-storage`, `GET/POST/DELETE local-storage(?problemId=&fileName=)`, `GET local-storage/ac`, `GET local-storage/content` |
| `ProgrammingLanguagesController` | `/api/ProgrammingLanguages` | `GET` |
| `StaticsController` | `/api/Statics` | `GET GetLast365DaysSubmissionsByMonth`, `GET GetSubmissionsByLanguageAsync` |
| `ScheduleController` | `/api/schedule-management/schedules` | `GET teachers`, `PUT {id}`, `DELETE {id}`, `GET`, `POST` |
| `TeachersController` | `/api/schedule-management/teachers` | `POST`, `PUT {id}`, `DELETE {id}`, `GET` |
| `SubjectsController` | `/api/schedule-management/subjects` | `POST`, `PUT {id}`, `DELETE {id}`, `GET` |
| `SubjectAssistantsController` | `/api/schedule-management/subjects/{subjectId}/assistant` | `GET`, `PUT` |

## 4. Flujos documentados a fondo en esta sesión

- **Creación de curso**: `AcademicController.CreateCourseAsync` → `AcademicService`
  (valida rol + nombre) → `AcademicRepository.CreateCourseAsync` (genera `CourseKey`
  único, `InviteCode`, crea `DbCourse` + `DbCourseUser` con rol `Teacher`). Ver gap de
  `InstitutionId` arriba.
- **Lanzamiento del IDE**: el JWT de lanzamiento (`IdeLaunchClaims`: sub, site_id,
  problem_id, contest_id, num, allowed_languages) se firmaba en el navegador
  (`juezvirtualbo-client-front`) con un secreto embebido en el bundle público —
  vulnerabilidad crítica. El fix está en el **PR #8, sin mergear a `develop` al
  escribir este documento**: agrega `IdeLaunchTokenController` (`POST
  patito-ide/launch-token`, `[Authorize]`) que firma server-side con
  `PATITO_IDE_TOKEN_SECRET`/`PATITO_IDE_TOKEN_ISS`/`PATITO_IDE_TOKEN_AUD`/
  `PATITO_IDE_TOKEN_TTL_SECONDS` (env vars, ver `docker-compose.yml` de la raíz).
  `IdeContextController.GetContextAsync` sigue validando ese mismo token vía
  `IIdeLaunchTokenValidator.Validate` (esa parte del código sí está en `develop`,
  no cambió). **Confirmar que el PR #8 esté mergeado antes de asumir que este
  endpoint existe en el `develop` que se esté leyendo.**
- **Envío de soluciones**: el camino real usado por el portal es
  `PublicController.SubmitAsync` (`POST public/submit`), que soporta
  `ContestProblemId` + `CourseId` + `AssignmentId` simultáneamente. `SubmissionController`
  (`POST Submission`) es un camino alternativo con un DTO más limitado (sin
  `ContestProblemId`) que **no tiene consumidores activos** en los frontends de este
  workspace a la fecha de este documento.

## 5. Validación

```
dotnet build OnlineJudgeAdmin.sln
dotnet test OnlineJudgeAdmin.sln
```

Si se corre dentro de `docker compose run patito-api ...`, el entorno del contenedor
inyecta `PATITO_IDE_TOKEN_SECRET` real (ver `docker-compose.yml` de la raíz), lo que
hace fallar los tests de `InfrastructureServiceTests` que asumen que esa variable NO
está seteada (usan su propio secreto de prueba vía `IConfiguration`). Para una corrida
limpia, usar un contenedor `dotnet/sdk` sin el entorno de compose, o exportar
`PATITO_IDE_TOKEN_SECRET=` (vacío) antes de correr los tests dentro de compose.

## 6. Riesgos/brechas observadas

- `InstitutionId` fantasma en la creación de cursos (ver §3).
- `SubmissionController.SubmitAsync` es código server-side sin consumidor conocido y
  con un DTO más pobre que el de `PublicController` — candidato a eliminar o
  unificar.
- `main` vs `develop` divergentes (170 commits) — riesgo de que un PR o un despliegue
  apunte a la rama equivocada.
- Los tests de `InfrastructureServiceTests` relacionados al token del IDE son frágiles
  ante variables de entorno del proceso (ver §5).
