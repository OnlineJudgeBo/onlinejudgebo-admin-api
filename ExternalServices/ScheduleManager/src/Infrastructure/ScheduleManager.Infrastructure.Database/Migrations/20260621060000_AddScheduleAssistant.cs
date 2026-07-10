using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ScheduleManager.Infrastructure.Database;

#nullable disable

namespace ScheduleManager.Infrastructure.Database.Migrations;

[DbContext(typeof(ScheduleDbContext))]
[Migration("20260621060000_AddScheduleAssistant")]
public partial class AddScheduleAssistant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "assistant_id", table: "schedules", type: "int(11)", nullable: true);
        migrationBuilder.CreateIndex(name: "assistant_id", table: "schedules", column: "assistant_id");
        migrationBuilder.AddForeignKey(name: "fk_schedules_assistants", table: "schedules", column: "assistant_id", principalTable: "teachers", principalColumn: "id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "fk_schedules_assistants", table: "schedules");
        migrationBuilder.DropIndex(name: "assistant_id", table: "schedules");
        migrationBuilder.DropColumn(name: "assistant_id", table: "schedules");
    }
}
