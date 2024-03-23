using System.ComponentModel.DataAnnotations;

namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class Problem
{
    public int? ProblemId { get; set; }

    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(100, ErrorMessage = "El título no debe exceder los 100 caracteres.")]
    public string? Title { get; set; } = null!;

    [Required(ErrorMessage = "La descripción del problema es obligatoria.")]
    [StringLength(5000, ErrorMessage = "La descripción no debe exceder los 5000 caracteres.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "La descripción de la entrada es obligatoria.")]
    [StringLength(2000, ErrorMessage = "La descripción de la entrada no debe exceder los 2000 caracteres.")]
    public string? Input { get; set; }

    [Required(ErrorMessage = "La descripción de la salida es obligatoria.")]
    [StringLength(2000, ErrorMessage = "La descripción de la salida no debe exceder los 2000 caracteres.")]
    public string? Output { get; set; }

    [Required(ErrorMessage = "Los ejemplos de entrada son obligatorios.")]
    [StringLength(2000, ErrorMessage = "Los ejemplos de entrada no deben exceder los 2000 caracteres.")]
    public string? SampleInput { get; set; }

    [Required(ErrorMessage = "Los ejemplos de salida son obligatorios.")]
    [StringLength(2000, ErrorMessage = "Los ejemplos de salida no deben exceder los 2000 caracteres.")]
    public string? SampleOutput { get; set; }

    public string? Spj { get; set; } = null!;

    [StringLength(1000, ErrorMessage = "Las notas no deben exceder los 1000 caracteres.")]
    public string? Hint { get; set; }

    [Required(ErrorMessage = "El autor es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre del autor no debe exceder los 100 caracteres.")]
    public string? Source { get; set; }

    public DateTime? InDate { get; set; }

    [Required(ErrorMessage = "El tiempo límite de ejecución es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El tiempo límite debe ser un número positivo.")]
    public int? TimeLimit { get; set; }

    [Required(ErrorMessage = "El límite de memoria es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El límite de memoria debe ser un número positivo.")]
    public int? MemoryLimit { get; set; }

    public string? Defunct { get; set; } = null!;

    public int? Accepted { get; set; }

    public int? Submit { get; set; }

    public int? Solved { get; set; }

    public virtual ICollection<ContestProblem>? ContestProblems { get; set; }
    public virtual ICollection<Solution>? Solutions { get; set; }
    public virtual ICollection<Classification>? Classifications { get; set; }
}
