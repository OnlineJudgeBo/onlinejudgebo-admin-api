namespace OnlineJudgeAdmin.Core.Domain.Models;

// An answer of the control-server, still open so its content can be streamed.
public sealed class ControlServerResponse(int statusCode, string? contentType, string? contentDisposition, Stream content, IDisposable owner) : IDisposable
{
    public int StatusCode { get; } = statusCode;
    public string? ContentType { get; } = contentType;
    public string? ContentDisposition { get; } = contentDisposition;
    public Stream Content { get; } = content;

    public bool Success => StatusCode is >= 200 and < 300;

    public void Dispose() => owner.Dispose();
}

// Response is set when the call reached the control-server; otherwise StatusCode and Message say why not.
public record MachinesForwardResult(int StatusCode, string? Message = null, ControlServerResponse? Response = null);
