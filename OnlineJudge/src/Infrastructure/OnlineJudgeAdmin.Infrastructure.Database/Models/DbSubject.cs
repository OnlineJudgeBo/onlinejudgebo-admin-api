using System.ComponentModel.DataAnnotations;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models
{
    public partial class DbSubject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;

        public virtual ICollection<DbSchedule> Schedules { get; set; } = new HashSet<DbSchedule>();
    }
}
