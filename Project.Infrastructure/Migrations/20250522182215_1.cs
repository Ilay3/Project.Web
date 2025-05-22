using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StageEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StageExecutionId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EventTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OperatorId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OperatorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AdditionalData = table.Column<string>(type: "text", nullable: false),
                    IsAutomatic = table.Column<bool>(type: "boolean", nullable: false),
                    PreviousMachineId = table.Column<int>(type: "integer", nullable: true),
                    NewMachineId = table.Column<int>(type: "integer", nullable: true),
                    DurationInPreviousState = table.Column<TimeSpan>(type: "interval", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageEvents_Machines_NewMachineId",
                        column: x => x.NewMachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StageEvents_Machines_PreviousMachineId",
                        column: x => x.PreviousMachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StageEvents_StageExecutions_StageExecutionId",
                        column: x => x.StageExecutionId,
                        principalTable: "StageExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EventTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    RelatedEntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdditionalData = table.Column<string>(type: "text", nullable: false),
                    StackTrace = table.Column<string>(type: "text", nullable: false),
                    InnerException = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessedTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProcessingAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextProcessingAttempt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StageEvents_EventTimeUtc",
                table: "StageEvents",
                column: "EventTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StageEvents_EventTimeUtc_EventType",
                table: "StageEvents",
                columns: new[] { "EventTimeUtc", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_StageEvents_EventType",
                table: "StageEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_StageEvents_IsAutomatic",
                table: "StageEvents",
                column: "IsAutomatic");

            migrationBuilder.CreateIndex(
                name: "IX_StageEvents_NewMachineId",
                table: "StageEvents",
                column: "NewMachineId");

            migrationBuilder.CreateIndex(
                name: "IX_StageEvents_OperatorId",
                table: "StageEvents",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_StageEvents_PreviousMachineId",
                table: "StageEvents",
                column: "PreviousMachineId");

            migrationBuilder.CreateIndex(
                name: "IX_StageEvents_StageExecutionId",
                table: "StageEvents",
                column: "StageExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_Category",
                table: "SystemEvents",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_Category_Severity",
                table: "SystemEvents",
                columns: new[] { "Category", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_EventTimeUtc",
                table: "SystemEvents",
                column: "EventTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_EventTimeUtc_Category_Severity",
                table: "SystemEvents",
                columns: new[] { "EventTimeUtc", "Category", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_EventType",
                table: "SystemEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_IsProcessed",
                table: "SystemEvents",
                column: "IsProcessed");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_IsProcessed_Severity",
                table: "SystemEvents",
                columns: new[] { "IsProcessed", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_Severity",
                table: "SystemEvents",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_Source",
                table: "SystemEvents",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_UserId",
                table: "SystemEvents",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StageEvents");

            migrationBuilder.DropTable(
                name: "SystemEvents");
        }
    }
}
