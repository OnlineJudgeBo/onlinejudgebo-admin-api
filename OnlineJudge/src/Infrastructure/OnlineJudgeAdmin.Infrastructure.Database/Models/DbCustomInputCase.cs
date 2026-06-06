using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("custom_input_case")]
public partial class DbCustomInputCase
{
    [Column("solution_id")]
    public int SolutionId { get; set; }

    [Column("case_number")]
    public int CaseNumber { get; set; }

    [Column("input_text")]
    public string InputText { get; set; } = string.Empty;

    [Column("expected_output")]
    public string? ExpectedOutput { get; set; }

    public virtual DbCustomInput CustomInput { get; set; } = null!;
}
