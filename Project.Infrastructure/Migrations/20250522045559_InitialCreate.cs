using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Project.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Details",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Details", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MachineTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Batches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DetailId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Batches_Details_DetailId",
                        column: x => x.DetailId,
                        principalTable: "Details",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DetailId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Routes_Details_DetailId",
                        column: x => x.DetailId,
                        principalTable: "Details",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    InventoryNumber = table.Column<string>(type: "text", nullable: false),
                    MachineTypeId = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Machines_MachineTypes_MachineTypeId",
                        column: x => x.MachineTypeId,
                        principalTable: "MachineTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubBatches_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RouteStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RouteId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    MachineTypeId = table.Column<int>(type: "integer", nullable: false),
                    NormTime = table.Column<double>(type: "double precision", nullable: false),
                    SetupTime = table.Column<double>(type: "double precision", nullable: false),
                    StageType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteStages_MachineTypes_MachineTypeId",
                        column: x => x.MachineTypeId,
                        principalTable: "MachineTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RouteStages_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SetupTimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MachineId = table.Column<int>(type: "integer", nullable: false),
                    FromDetailId = table.Column<int>(type: "integer", nullable: false),
                    ToDetailId = table.Column<int>(type: "integer", nullable: false),
                    Time = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupTimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetupTimes_Details_FromDetailId",
                        column: x => x.FromDetailId,
                        principalTable: "Details",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupTimes_Details_ToDetailId",
                        column: x => x.ToDetailId,
                        principalTable: "Details",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupTimes_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StageExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubBatchId = table.Column<int>(type: "integer", nullable: false),
                    RouteStageId = table.Column<int>(type: "integer", nullable: false),
                    MachineId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PauseTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResumeTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsSetup = table.Column<bool>(type: "boolean", nullable: false),
                    QueuePosition = table.Column<int>(type: "integer", nullable: true),
                    ScheduledStartTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    OperatorId = table.Column<string>(type: "text", nullable: true),
                    ReasonNote = table.Column<string>(type: "text", nullable: true),
                    IsProcessedByScheduler = table.Column<bool>(type: "boolean", nullable: false),
                    StatusChangedTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastErrorMessage = table.Column<string>(type: "text", nullable: true),
                    DeviceId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageExecutions_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StageExecutions_RouteStages_RouteStageId",
                        column: x => x.RouteStageId,
                        principalTable: "RouteStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StageExecutions_SubBatches_SubBatchId",
                        column: x => x.SubBatchId,
                        principalTable: "SubBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Batches_DetailId",
                table: "Batches",
                column: "DetailId");

            migrationBuilder.CreateIndex(
                name: "IX_Details_Number",
                table: "Details",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_InventoryNumber",
                table: "Machines",
                column: "InventoryNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_MachineTypeId",
                table: "Machines",
                column: "MachineTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineTypes_Name",
                table: "MachineTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_DetailId",
                table: "Routes",
                column: "DetailId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStages_MachineTypeId",
                table: "RouteStages",
                column: "MachineTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStages_RouteId",
                table: "RouteStages",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupTimes_FromDetailId",
                table: "SetupTimes",
                column: "FromDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupTimes_MachineId_FromDetailId_ToDetailId",
                table: "SetupTimes",
                columns: new[] { "MachineId", "FromDetailId", "ToDetailId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SetupTimes_ToDetailId",
                table: "SetupTimes",
                column: "ToDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_IsSetup",
                table: "StageExecutions",
                column: "IsSetup");

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_MachineId",
                table: "StageExecutions",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_RouteStageId",
                table: "StageExecutions",
                column: "RouteStageId");

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_StartTimeUtc",
                table: "StageExecutions",
                column: "StartTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_Status",
                table: "StageExecutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StageExecutions_SubBatchId",
                table: "StageExecutions",
                column: "SubBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SubBatches_BatchId",
                table: "SubBatches",
                column: "BatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SetupTimes");

            migrationBuilder.DropTable(
                name: "StageExecutions");

            migrationBuilder.DropTable(
                name: "Machines");

            migrationBuilder.DropTable(
                name: "RouteStages");

            migrationBuilder.DropTable(
                name: "SubBatches");

            migrationBuilder.DropTable(
                name: "MachineTypes");

            migrationBuilder.DropTable(
                name: "Routes");

            migrationBuilder.DropTable(
                name: "Batches");

            migrationBuilder.DropTable(
                name: "Details");
        }
    }
}
