using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Core.Domain.Models;
public class Tag
{
    public int TagId { get; set; }
    public string TagName { get; set; }
}

