using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Core.Domain.Models;

public class CompileInfo
{
    public int SolutionId { get; set; }

    public string Error { get; set; }
}
