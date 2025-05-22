using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ReasonNote",
                table: "StageExecutions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OperatorId",
                table: "StageExecutions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastErrorMessage",
                table: "StageExecutions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceId",
                table: "StageExecutions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_IsProcessedByScheduler",
                table: "StageExecutions",
                column: "IsProcessedByScheduler");

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_MachineId_Status_QueuePosition",
                table: "StageExecutions",
                columns: new[] { "MachineId", "Status", "QueuePosition" });

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_ScheduledStartTimeUtc",
                table: "StageExecutions",
                column: "ScheduledStartTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_Status_Priority_ScheduledStartTimeUtc",
                table: "StageExecutions",
                columns: new[] { "Status", "Priority", "ScheduledStartTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_StatusChangedTimeUtc",
                table: "StageExecutions",
                column: "StatusChangedTimeUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StageExecutions_IsProcessedByScheduler",
                table: "StageExecutions");

            migrationBuilder.DropIndex(
                name: "IX_StageExecutions_MachineId_Status_QueuePosition",
                table: "StageExecutions");

            migrationBuilder.DropIndex(
                name: "IX_StageExecutions_ScheduledStartTimeUtc",
                table: "StageExecutions");

            migrationBuilder.DropIndex(
                name: "IX_StageExecutions_Status_Priority_ScheduledStartTimeUtc",
                table: "StageExecutions");

            migrationBuilder.DropIndex(
                name: "IX_StageExecutions_StatusChangedTimeUtc",
                table: "StageExecutions");

            migrationBuilder.AlterColumn<string>(
                name: "ReasonNote",
                table: "StageExecutions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OperatorId",
                table: "StageExecutions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastErrorMessage",
                table: "StageExecutions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceId",
                table: "StageExecutions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
