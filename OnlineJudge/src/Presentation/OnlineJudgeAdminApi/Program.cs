using OnlineJudgeAdmin.Infrastructure.Database.DependencyInjection;
using OnlineJudgeAdmin.Infrastructure.AwsS3.DependencyInjection;
using OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager.DependencyInjection;
using OnlineJudgeAdmin.Infrastructure.IdeIntegration.DependencyInjection;
using OnlineJudgeAdmin.Core.Application.Validators.DependencyInjection;
using OnlineJudgeAdmin.Core.Application.Services.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdminApi.ExceptionHandler;
using OnlineJudgeAdminApi.Helpers;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using ScheduleManager.Infrastructure.Database.DependencyInjection;
using ScheduleManager.Infrastructure.Database;
using ScheduleManager.Core.Application.Services.DependencyInjection;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/api/patito-ide"))
                {
                    context.NoResult();
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("Authentication failed: " + context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();

                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var result = JsonSerializer.Serialize(new { error = "Token inválido o expirado" });
                return context.Response.WriteAsync(result);
            },
        };
    });

builder.Services.AddControllers()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.Converters.Add(new DateTimeConverterUsingDateTimeParse());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
    Microsoft.EntityFrameworkCore.ServerVersion.Parse("11.2.2-mariadb"),
    mySqlOptions => mySqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
    .LogTo(Console.WriteLine, LogLevel.Information));

builder.Services.AddDbContext<ScheduleDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("ScheduleConnection"),
    Microsoft.EntityFrameworkCore.ServerVersion.Parse("11.2.2-mariadb"),
    mySqlOptions => mySqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
    .LogTo(Console.WriteLine, LogLevel.Information));

//builder.Services.AddHttpContextAccessor();
builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly());
builder.Services.AddDatabaseRepositories(builder.Configuration);
builder.Services.AddScheduleDatabaseRepositories(builder.Configuration);
builder.Services.AddApplicationValidators();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddApplicationScheduleServices(builder.Configuration);
builder.Services.AddFileSystemLocalManagerInfrastructureManager(builder.Configuration);
builder.Services.AddAwsS3FileManager(builder.Configuration);
builder.Services.AddIdeIntegrationInfrastructure(builder.Configuration);
builder.Services.AddScoped<UserClaimsHelper>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.ExecuteSqlRaw("""
        ALTER TABLE problems_site
          ADD COLUMN IF NOT EXISTS is_active TINYINT(1) NOT NULL DEFAULT 1;
        """);
    dbContext.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS custom_input (
          solution_id INT NOT NULL,
          problem_id INT NOT NULL,
          user_id VARCHAR(48) NOT NULL,
          site_id INT NOT NULL,
          created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
          PRIMARY KEY (solution_id),
          KEY custom_input_problem_id (problem_id),
          KEY custom_input_user_id (user_id),
          KEY custom_input_site_id (site_id)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
        """);
    dbContext.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS custom_input_case (
          solution_id INT NOT NULL,
          case_number INT NOT NULL,
          input_text MEDIUMTEXT NOT NULL,
          expected_output MEDIUMTEXT NULL,
          PRIMARY KEY (solution_id, case_number)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
        """);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

app.UseCors(corsBuilder =>
{
    if (allowedOrigins.Length > 0)
    {
        corsBuilder.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        return;
    }

    corsBuilder.SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
});

app.UseMiddleware<ExceptionHandler>();

//app.UseCors("AllowAnyOrigin");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
