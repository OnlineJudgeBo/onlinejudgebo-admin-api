using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Core.Domain.Models;


public class Online
{
    public string? Hash { get; set; }

    public string? IP { get; set; }

    public string? UA { get; set; }

    public string? Refer { get; set; }

    public int LastMove { get; set; }

    public int? FirstTime { get; set; }

    public string? Uri { get; set; }
}
