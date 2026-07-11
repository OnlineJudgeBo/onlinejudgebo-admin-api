# onlineJudgeAdmin-back

Backend de Patito Online Judge, la arquitectura que se usa es Onion, el dominio queda al centro, la aplicación orquesta casos de uso, y los detalles externos HTTP, EF Core, MariaDB, S3, filesystem e integración IDE, viven en las capas de afuera.

## Stack

- .NET 8 / ASP.NET Core
- MariaDB con Entity Framework Core y Pomelo
- JWT Bearer para autenticación
- Swagger en desarrollo
- Docker para publicación de la API

Archivos útiles:

- `OnlineJudgeAdmin.sln`: solución principal.
- `ENDPOINTS.md`: resumen de endpoints.
- `docs/PROJECT_STRUCTURE_GUIDELINES.md`: reglas internas de estructura.
- `Dockerfile`: build y publish de la API.

## Estructura

```text
.
├── OnlineJudgeAdmin.sln
├── OnlineJudge/
│   ├── src/
│   │   ├── Core/
│   │   │   ├── Domain/
│   │   │   │   ├── Models/
│   │   │   │   └── Abstractions/
│   │   │   └── Application/
│   │   │       ├── Services/
│   │   │       └── Validators/
│   │   ├── Infrastructure/
│   │   │   ├── OnlineJudgeAdmin.Infrastructure.Database/
│   │   │   ├── OnlineJudgeAdmin.Infrastructure.AwsS3/
│   │   │   ├── OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager/
│   │   │   └── OnlineJudgeAdmin.Infrastructure.IdeIntegration/
│   │   └── Presentation/
│   │       └── OnlineJudgeAdminApi/
│   └── Tests/
│       └── Core/Application/Services/
├── scripts/database/
```

## Arquitectura Onion en este proyecto

La dependencia siempre apunta hacia adentro. Las capas externas conocen a las internas; el dominio no conoce a nadie.

```text
Presentation  ─┐
               ├──> Application ───> Domain Abstractions ───> Domain Models
Infrastructure ┘
```


- `Domain.Models` no depende de ASP.NET, EF Core ni infraestructura.
- `Domain.Abstractions` declara contratos de repositorios, servicios e integraciones.
- `Application.Services` implementa casos de uso usando esos contratos.
- `Infrastructure.*` implementa persistencia e integraciones externas.
- `Presentation/OnlineJudgeAdminApi` expone HTTP y compone las dependencias en `Program.cs`.

La API referencia proyectos de infraestructura porque ahí se arma el contenedor de dependencias. Eso no habilita a que un controller use un `DbContext` o una implementación concreta directamente.

## Capas

### Domain

Ruta:

```text
OnlineJudge/src/Core/Domain
```

Es el centro de la solución.

`Models` contiene los modelos usados por el negocio: problemas, concursos, usuarios, soluciones, cursos, rankings, reportes, schedule, modelos públicos y modelos de Patito IDE.

`Abstractions` contiene los puertos de la aplicación:

```text
Infrastructure/   contratos para S3, filesystem, IDE, correo, etc.
Repositories/     contratos de persistencia
Services/         contratos de casos de uso
```

Regla simple: si una clase del dominio necesita importar algo de EF Core, ASP.NET o una integración externa, está en la capa equivocada.

### Application

Ruta:

```text
OnlineJudge/src/Core/Application
```

Aquí están los casos de uso. Esta capa coordina reglas de negocio, validaciones y llamadas a repositorios o servicios externos a través de interfaces.

Proyectos:

- `OnlineJudgeAdmin.Core.Application.Services`
- `OnlineJudgeAdmin.Core.Application.Validators`

Servicios representativos:

```text
AcademicService              ProblemService
ContestService               PublicService
FileManagerService           ScheduleService
IdeContextService            SolutionService
IdeSubmissionService         TopicService
JudgeService                 UserService
```

Las implementaciones se registran en:

```text
OnlineJudge/src/Core/Application/Services/DependencyInjection/ServiceCollectionExtensions.cs
```

### Infrastructure

Ruta:

```text
OnlineJudge/src/Infrastructure
```

Contiene adaptadores hacia recursos externos:

- `OnlineJudgeAdmin.Infrastructure.Database`: EF Core, DbContexts, modelos `Db*`, mappers y repositorios.
- `OnlineJudgeAdmin.Infrastructure.AwsS3`: almacenamiento en S3.
- `OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager`: manejo de archivos locales.
- `OnlineJudgeAdmin.Infrastructure.IdeIntegration`: soporte para tokens/contexto de Patito IDE.

Contextos de base de datos usados por la API:

- `AppDbContext`: base principal del judge/admin.
- `AcademicCatalogDbContext`: catálogo académico.
- `ScheduleManagementDbContext`: módulo de horarios.

Los modelos de EF se mantienen separados de los modelos de dominio para no meter detalles de tablas, columnas o relaciones dentro del centro de la aplicación.

### Presentation

Ruta:

```text
OnlineJudge/src/Presentation/OnlineJudgeAdminApi
```

Es la API ASP.NET Core.

Contiene:

- `Program.cs`: configuración de middleware, autenticación, CORS, Swagger, DbContexts y DI.
- `Controllers/`: endpoints HTTP.
- `DataTransferObjects/`: contratos de entrada y salida del API.
- `Mappers/`: perfiles para mapeo de DTOs.
- `ExceptionHandler/`: manejo centralizado de errores.
- `Helpers/`: utilidades para claims, IP, autenticación y request context.

Controllers actuales:

```text
AcademicController              PublicController
ContestsController              PublicAuthController
FileManagerController           RolesController
IdeContextController            ScheduleController
JudgeController                 StaticsController
ProblemsController              SubjectAssistantsController
ProgrammingLanguagesController  SubjectsController
SubmissionController            TeachersController
TopicsController                UsersController
```

Un controller debería hacer poco: leer HTTP, delegar al caso de uso y responder. La lógica larga pertenece a Application.

## Flujo de una petición

```text
HTTP
  -> Controller
  -> Servicio de aplicación
  -> Puerto de dominio (repositorio / integración)
  -> Adaptador de infraestructura
  -> MariaDB, S3, filesystem o servicio externo
```

Ejemplo con problemas:

```text
ProblemsController
  -> IProblemService / ProblemService
  -> IProblemRepository
  -> ProblemRepository
  -> AppDbContext + modelos DbProblem, DbProblemSite, DbProblemTag...
```

## Módulos principales

- **Administración:** problemas, usuarios, roles, concursos, lenguajes, juez, estadísticas y archivos.
- **API pública:** dashboard, problemas visibles, concursos, rankings, actividad, login, registro y recuperación de contraseña.
- **Académico:** instituciones, cursos, miembros, tareas, reportes y progreso de rutas.
- **Patito IDE:** contexto de problema, ejecución de pruebas, envíos y tokens de lanzamiento.
- **Schedule management:** materias, horarios, docentes y auxiliares dentro de la misma API.

## Reglas de dependencia

Permitido:

```text
Application -> Domain
Infrastructure -> Domain
Presentation -> Application
Presentation -> Infrastructure    solo para registrar dependencias
Tests -> capa bajo prueba
```

Evitar:

```text
Domain -> Infrastructure
Domain -> Presentation
Application -> Presentation
Application -> implementaciones concretas de Infrastructure
Infrastructure -> Presentation
Controller -> DbContext directo
```

## Configuración

Archivos base:

```text
OnlineJudge/src/Presentation/OnlineJudgeAdminApi/appsettings.json
OnlineJudge/src/Presentation/OnlineJudgeAdminApi/appsettings.Development.json
```

Secciones importantes:

- `ConnectionStrings`: base de datos.
- `Jwt`: issuer, audience y llave de firma.
- `Base`: URL base y bucket de almacenamiento.
- `FileSettings`: ruta local para archivos de problemas.
- `PatitoIde`: configuración de integración con el IDE.
- `Cors:AllowedOrigins`: orígenes permitidos; si no está definido, el backend queda abierto a cualquier origen según la configuración actual.

En ambientes reales, los secretos deben entrar por variables de entorno o por el mecanismo del despliegue. No deberían quedar fijos en archivos versionados.


## Ejecución local

Desde la raíz:

```bash
dotnet restore OnlineJudgeAdmin.sln
dotnet run --project OnlineJudge/src/Presentation/OnlineJudgeAdminApi/OnlineJudgeAdminApi.csproj
```

Con la configuración actual, la API escucha en `http://+:8088`. En desarrollo queda disponible Swagger.

## Pruebas

```bash
dotnet test OnlineJudgeAdmin.sln
```

Las pruebas viven en:

```text
OnlineJudge/Tests/Core/Application/Services
```

Cubren servicios de aplicación como académico, concursos, dominio, IDE, problemas, público, horarios y usuarios.

## Docker

```bash
docker build -t onlinejudge-admin-api .
docker run --rm -p 8088:8088 onlinejudge-admin-api
```

El contenedor final ejecuta:

```text
dotnet OnlineJudgeAdminApi.dll
```

## CI/CD

`.github/workflows/deploy.yml` se ejecuta sobre `develop` y hace, en resumen:

1. Restore, build y pruebas unitarias.
2. Despliegue del backend con Docker Compose en runner self-hosted.
3. Pruebas Selenium BDD desde el repositorio de automatización.

## Cómo agregar una funcionalidad

Orden recomendado:

1. Modelos o contratos nuevos en `Core/Domain`.
2. Caso de uso en `Core/Application/Services`.
3. Implementación de repositorio o integración en `Infrastructure`.
4. Registro en las extensiones de `DependencyInjection`.
5. Controller y DTOs en `Presentation`.
6. Pruebas en `OnlineJudge/Tests`.

Si una feature nueva obliga a saltarse ese orden, conviene revisar primero si falta un contrato en el dominio o si se está filtrando un detalle de infraestructura hacia una capa interna.
