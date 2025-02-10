using ScheduleManager.Infrastructure.Database.DependencyInjection;
using ScheduleManager.Infrastructure.AwsS3.DependencyInjection;
using ScheduleManager.Infrastructure.FileSystemLocalManager.DependencyInjection;
using ScheduleManager.Core.Application.Validators.DependencyInjection;
using ScheduleManager.Core.Application.Services.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdminApi.ExceptionHandler;
using OnlineJudgeAdminApi.Helpers;
using ScheduleManager.Infrastructure.Database;

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
    Microsoft.EntityFrameworkCore.ServerVersion.Parse("11.2.2-mariadb"))
    .LogTo(Console.WriteLine, LogLevel.Information));

builder.Services.AddDbContext<ScheduleDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("ScheduleConnection"),
    Microsoft.EntityFrameworkCore.ServerVersion.Parse("11.2.2-mariadb"))
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
builder.Services.AddScoped<UserClaimsHelper>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors(builder =>
{
    builder.AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod();
});

app.UseMiddleware<ExceptionHandler>();

//app.UseCors("AllowAnyOrigin");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
