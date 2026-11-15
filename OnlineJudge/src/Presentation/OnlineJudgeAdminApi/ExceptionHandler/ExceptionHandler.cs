using System.Net;
using System.Text.RegularExpressions;
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
        catch (Exception ex) when (DescribeDatabaseError(ex) is { } reason)
        {
            await this.HandleDatabaseErrorAsync(httpContext, ex, reason);
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

    private async Task HandleDatabaseErrorAsync(HttpContext context, Exception ex, string reason)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        this.logger.LogInformation(ex.ToString());

        await context.Response.WriteAsync(new ErrorDetails
        {
            StatusCode = context.Response.StatusCode,
            Message = $"No se pudo completar la operación: {reason}",
        }.ToString());
    }

    // Database errors caused by the data sent, in words the user can act on; the rest stay "error interno".
    // EF wraps the MySqlException in a DbUpdateException, so the inner exception is checked too.
    private static string? DescribeDatabaseError(Exception ex)
    {
        var message = (ex as MySqlException ?? ex.InnerException as MySqlException)?.Message;
        if (message is null)
        {
            return null;
        }

        var column = Regex.Match(message, "column '([^']+)'", RegexOptions.IgnoreCase).Groups[1].Value;
        if (message.StartsWith("Data too long", StringComparison.OrdinalIgnoreCase)) return $"el campo '{column}' es demasiado largo.";
        if (message.StartsWith("Out of range", StringComparison.OrdinalIgnoreCase)) return $"el valor del campo '{column}' está fuera de rango.";
        if (message.StartsWith("Incorrect", StringComparison.OrdinalIgnoreCase) && column.Length > 0) return $"el campo '{column}' tiene un valor inválido.";
        if (message.Contains("cannot be null", StringComparison.OrdinalIgnoreCase)) return $"el campo '{column}' es obligatorio.";
        if (message.StartsWith("Duplicate entry", StringComparison.OrdinalIgnoreCase)) return "ya existe un registro con ese valor.";
        if (message.StartsWith("Cannot add or update a child row", StringComparison.OrdinalIgnoreCase)) return "hace referencia a un registro que no existe.";
        if (message.StartsWith("Cannot delete or update a parent row", StringComparison.OrdinalIgnoreCase)) return "otros registros dependen de este.";
        return null;
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
