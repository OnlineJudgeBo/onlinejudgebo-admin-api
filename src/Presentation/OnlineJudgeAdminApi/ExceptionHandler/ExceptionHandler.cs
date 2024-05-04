using System.Net;
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
                messageToUse = $"Cannot complete the operation, {ex.Message}";
                this.logger.LogInformation(ex.ToString());
                break;
            case Exceptions.UnauthorizedAccess:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                messageToUse = "Cannot complete the operation, unauthorized request";
                this.logger.LogInformation(ex.ToString());
                break;
            case Exceptions.AllTheOther:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                messageToUse = "Cannot complete the operation, internal error";
                this.logger.LogError(ex.ToString());
                break;
        }

        await context.Response.WriteAsync(new ErrorDetails()
        {
            StatusCode = context.Response.StatusCode,
            Message = messageToUse,
        }.ToString());
    }
}
