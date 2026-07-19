namespace OnlineJudgeAdmin.Core.Domain.Models;

public readonly record struct JudgeVerdictInfo(
    string StatusKey,
    string StatusLabel,
    string GeneralStatusKey,
    string GeneralStatusLabel,
    bool IsFinal);

public static class JudgeResultCodes
{
    public const short Pending = 0;
    public const short WaitRejudge = 1;
    public const short Compiling = 2;
    public const short RunningAndJudging = 3;
    public const short Accepted = 4;
    public const short PresentationError = 5;
    public const short WrongAnswer = 6;
    public const short TimeLimitExceeded = 7;
    public const short MemoryLimitExceeded = 8;
    public const short OutputLimitExceeded = 9;
    public const short RuntimeError = 10;
    public const short CompileError = 11;
    public const short CompileOk = 12;
    public const short TestRunDone = 13;
    public const short AiDetected = 14;
}

public static class JudgeVerdictCatalog
{
    public static JudgeVerdictInfo Map(short resultCode)
    {
        return resultCode switch
        {
            JudgeResultCodes.Pending => new JudgeVerdictInfo("pending", "Pending", "queued", "En cola", false),
            JudgeResultCodes.WaitRejudge => new JudgeVerdictInfo("pending_rejudge", "Pending Rejudging", "queued", "En cola", false),
            JudgeResultCodes.Compiling => new JudgeVerdictInfo("compiling", "Compiling", "evaluating", "En evaluacion", false),
            JudgeResultCodes.RunningAndJudging => new JudgeVerdictInfo("running", "Running & Judging", "evaluating", "En evaluacion", false),
            JudgeResultCodes.Accepted => new JudgeVerdictInfo("accepted", "Accepted", "finished", "Finalizado", true),
            JudgeResultCodes.PresentationError => new JudgeVerdictInfo("presentation_error", "Presentation Error", "finished", "Finalizado", true),
            JudgeResultCodes.WrongAnswer => new JudgeVerdictInfo("wrong_answer", "Wrong Answer", "finished", "Finalizado", true),
            JudgeResultCodes.TimeLimitExceeded => new JudgeVerdictInfo("time_limit_exceeded", "Time Limit Exceed", "finished", "Finalizado", true),
            JudgeResultCodes.MemoryLimitExceeded => new JudgeVerdictInfo("memory_limit_exceeded", "Memory Limit Exceed", "finished", "Finalizado", true),
            JudgeResultCodes.OutputLimitExceeded => new JudgeVerdictInfo("output_limit_exceeded", "Output Limit Exceed", "finished", "Finalizado", true),
            JudgeResultCodes.RuntimeError => new JudgeVerdictInfo("runtime_error", "Runtime Error", "finished", "Finalizado", true),
            JudgeResultCodes.CompileError => new JudgeVerdictInfo("compile_error", "Compile Error", "finished", "Finalizado", true),
            JudgeResultCodes.CompileOk => new JudgeVerdictInfo("compiled", "Compile OK", "evaluating", "En evaluacion", false),
            JudgeResultCodes.TestRunDone => new JudgeVerdictInfo("test_run", "Test Running Done", "evaluating", "En evaluacion", false),
            JudgeResultCodes.AiDetected => new JudgeVerdictInfo("ai_detected", "IA Detected", "finished", "Finalizado", true),
            _ => new JudgeVerdictInfo("unknown", $"Estado {resultCode}", "evaluating", "En evaluacion", false)
        };
    }
}
