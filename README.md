# API de administración de Patito

Backend del panel administrativo, la web pública y Patito IDE. Acá está la lógica de usuarios, problemas, concursos, cursos, envíos, reportes y archivos del juez.

Usa .NET 8, ASP.NET Core, Entity Framework Core y MariaDB.

## Ejecutar la API

Hace falta el SDK de .NET 8 y una base MariaDB con el esquema del juez.

```bash
dotnet restore OnlineJudgeAdmin.sln
dotnet run --project OnlineJudge/src/Presentation/OnlineJudgeAdminApi/OnlineJudgeAdminApi.csproj
```

Swagger se habilita en desarrollo en `/swagger`. El puerto sale de `launchSettings.json`, `ASPNETCORE_URLS` o de la configuración usada al arrancar.

## Dependencias

La API necesita MariaDB para arrancar con datos reales. También usa la carpeta compartida de problemas cuando se suben o editan casos de prueba.

Los demás proyectos se conectan así:

```text
juezvirtualbo-admin-front ─┐
patito-client-web ──────├─> API ─> MariaDB
patito-ide ────────────┘
```

El kernel no es necesario para abrir la API, pero sí para que los envíos lleguen a un veredicto.

Para levantar el sistema completo usa el Compose de la raíz:

```bash
cd ..
docker compose up -d --build
```

Compose espera que MariaDB esté lista antes de iniciar esta API.

## Configuración

Los archivos base son:

```text
OnlineJudge/src/Presentation/OnlineJudgeAdminApi/appsettings.json
OnlineJudge/src/Presentation/OnlineJudgeAdminApi/appsettings.Development.json
```

Ahí se configuran:

| Sección | Variable de entorno | Uso |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | base principal del juez |
| `ConnectionStrings:ScheduleConnection` | `ConnectionStrings__ScheduleConnection` | base del módulo de horarios |
| `Jwt:Issuer` | `Jwt__Issuer` | emisor aceptado en los tokens |
| `Jwt:Audience` | `Jwt__Audience` | audiencia de los tokens |
| `Jwt:Key` | `Jwt__Key` | clave de firma; debe ser secreta |
| `Base:Url` | `Base__Url` | URL pública del cliente web |
| `Base:BucketName` | `Base__BucketName` | bucket usado por la integración S3 |
| `FileSettings:ProblemsFilePath` | `FileSettings__ProblemsFilePath` | carpeta compartida con los problemas |

ASP.NET Core cambia los `:` por `__` en variables de entorno. Por ejemplo, `Jwt:Key` pasa a ser `Jwt__Key`.

En Docker se pueden pasar como variables de entorno:

```env
ConnectionStrings__DefaultConnection=server=localhost;database=jol;user=patito;pwd=...
Jwt__Key=una-clave-larga-y-segura
FileSettings__ProblemsFilePath=/home/judge/data
```

Las contraseñas reales no deben quedar en `appsettings.json`.

En Docker también se usan `ASPNETCORE_ENVIRONMENT` y `ASPNETCORE_URLS`. La primera elige el ambiente y la segunda indica en qué dirección y puerto escucha la API.

## Arquitectura

La solución está separada en cuatro partes:

```text
HTTP -> Presentation -> Application -> Domain
                         |
                         v
                   Infrastructure -> MariaDB, S3 y archivos
```

- **Domain** guarda los modelos y las interfaces. No conoce ASP.NET ni Entity Framework.
- **Application** implementa los casos de uso y trabaja contra esas interfaces.
- **Infrastructure** conecta las interfaces con MariaDB, S3, el filesystem y Patito IDE.
- **Presentation** recibe HTTP, valida el request y llama al servicio correspondiente.

`Program.cs` registra las implementaciones concretas. Por eso la API conoce los proyectos de infraestructura, pero los servicios de aplicación no dependen de ellos.

Una petición normalmente pasa por:

```text
Controller -> Service -> Repository -> DbContext -> MariaDB
```

Los controllers deben quedar cortos. No pongas consultas de EF Core ni reglas de negocio dentro de ellos.

## Estructura del proyecto

```text
.
├── OnlineJudge/
│   ├── src/
│   │   ├── Core/
│   │   │   ├── Domain/
│   │   │   │   ├── Models/         modelos del negocio
│   │   │   │   └── Abstractions/   repositorios y servicios
│   │   │   └── Application/
│   │   │       ├── Services/       casos de uso
│   │   │       └── Validators/     validaciones
│   │   ├── Infrastructure/
│   │   │   ├── ...Database/       EF Core y repositorios
│   │   │   ├── ...AwsS3/          almacenamiento S3
│   │   │   ├── ...FileSystem.../  archivos locales
│   │   │   └── ...IdeIntegration/ conexión con el IDE
│   │   └── Presentation/
│   │       └── OnlineJudgeAdminApi/
│   │           ├── Controllers/    endpoints HTTP
│   │           ├── DataTransferObjects/
│   │           ├── Mappers/
│   │           └── Program.cs      arranque y DI
│   └── Tests/                         pruebas unitarias
├── docs/                                  notas de estructura
├── scripts/database/                      scripts de base de datos
├── Dockerfile
└── OnlineJudgeAdmin.sln
```

## Pruebas

```bash
dotnet build OnlineJudgeAdmin.sln
dotnet test OnlineJudgeAdmin.sln
```

La lista de rutas está en [`ENDPOINTS.md`](ENDPOINTS.md). Para ubicar código nuevo, revisa [`docs/PROJECT_STRUCTURE_GUIDELINES.md`](docs/PROJECT_STRUCTURE_GUIDELINES.md).

## Docker

```bash
docker build -t patito-admin-api .
docker run --rm -p 8088:8080 patito-admin-api
```

Al contenedor hay que pasarle las conexiones, la configuración JWT y el volumen de problemas.
