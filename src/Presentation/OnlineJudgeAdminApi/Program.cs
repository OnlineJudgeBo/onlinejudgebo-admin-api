using OnlineJudgeAdmin.Infrastructure.Database.DependencyInjection;
using OnlineJudgeAdmin.Infrastructure.AwsS3.DependencyInjection;
using OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager.DependencyInjection;
using OnlineJudgeAdmin.Core.Application.Validators.DependencyInjection;
using OnlineJudgeAdmin.Core.Application.Services.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Reflection;
using Newtonsoft.Json;
using OnlineJudgeAdmin.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.IgnoreNullValues = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
                var result = JsonConvert.SerializeObject(new { error = "Token inválido o expirado" });
                return context.Response.WriteAsync(result);
            },
        };
    });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
    Microsoft.EntityFrameworkCore.ServerVersion.Parse("11.2.2-mariadb"))
    .LogTo(Console.WriteLine, LogLevel.Information));

builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly());
builder.Services.AddDatabaseRepositories(builder.Configuration);
builder.Services.AddApplicationValidators();
builder.Services.AddApplicationServices();
builder.Services.AddFileSystemLocalManagerInfrastructureManager(builder.Configuration);
builder.Services.AddAwsS3FileManager(builder.Configuration);
//builder.Services.Add
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
        .AllowAnyMethod()
        ;
});

//app.UseCors("AllowAnyOrigin");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

