using System.Net;
using MySqlConnector;
namespace OnlineJudgeAdminApi.ExceptionHandler;

public class ExceptionHandler
{
    private readonly RequestDelegate next;

    private readonly ILogger<ExceptionHandler> logger;

    public ExceptionHandler(RequestDelegate next, ILogger<ExceptionHandler> logger)
    {
        this.next = next ?? throw new ArgumentNullException(nameof(next));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await this.next(httpContext);
        }
        catch (ArgumentException ex)
        {
            await this.HandleExceptionAsync(httpContext, Exceptions.Argument, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            await this.HandleExceptionAsync(httpContext, Exceptions.UnauthorizedAccess, ex);
        }
        catch (InvalidOperationException ex)
        {
            await this.HandleExceptionAsync(httpContext, Exceptions.InvalidOperation, ex);
        }
        catch (KeyNotFoundException ex)
        {
            await this.HandleNotFoundExceptionAsync(httpContext, ex);
        }
        catch (MySqlException ex) when (this.IsMissingAcademicSchemaException(ex))
        {
            await this.HandleAcademicSchemaExceptionAsync(httpContext, ex);
        }
        catch (Exception ex)
        {
            await this.HandleExceptionAsync(httpContext, Exceptions.AllTheOther, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exceptions typeOfException, Exception ex)
    {
        context.Response.ContentType = "application/json";
        string messageToUse = string.Empty;
        switch (typeOfException)
        {
            case Exceptions.Argument or Exceptions.InvalidOperation:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                messageToUse = $"No se pudo completar la operación: {ex.Message}";
                this.logger.LogInformation(ex.ToString());
                break;
            case Exceptions.UnauthorizedAccess:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                messageToUse = "No se pudo completar la operación: solicitud no autorizada";
                this.logger.LogInformation(ex.ToString());
                break;
            case Exceptions.AllTheOther:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                messageToUse = "No se pudo completar la operación: error interno";
                this.logger.LogError(ex.ToString());
                break;
        }

        await context.Response.WriteAsync(new ErrorDetails()
        {
            StatusCode = context.Response.StatusCode,
            Message = messageToUse,
        }.ToString());
    }

    private async Task HandleNotFoundExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        this.logger.LogInformation(ex.ToString());

        await context.Response.WriteAsync(new ErrorDetails
        {
            StatusCode = context.Response.StatusCode,
            Message = $"No se pudo completar la operación: {ex.Message}",
        }.ToString());
    }

    private async Task HandleAcademicSchemaExceptionAsync(HttpContext context, MySqlException ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        const string message =
            "No se pudo completar la operación: las tablas académicas necesarias no están disponibles para esta solicitud.";

        this.logger.LogError(ex.ToString());

        await context.Response.WriteAsync(new ErrorDetails
        {
            StatusCode = context.Response.StatusCode,
            Message = message,
        }.ToString());
    }

    private bool IsMissingAcademicSchemaException(MySqlException ex)
    {
        if (!ex.Message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ex.Message.Contains("course", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("course_assignment", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("course_assignment_problem", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("course_submission_context", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("learning_path", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("topic", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("subtopic", StringComparison.OrdinalIgnoreCase);
    }
}
