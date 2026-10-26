using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIron.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityAndSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    DistanceKm = table.Column<double>(type: "double precision", nullable: true),
                    Steps = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsistencySettings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepGoal = table.Column<int>(type: "integer", nullable: false),
                    ScheduleSince = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsistencySettings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ConsistencySettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledDays",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    WorkoutTemplateId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledDays", x => new { x.UserId, x.DayOfWeek });
                    table.ForeignKey(
                        name: "FK_ScheduledDays_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduledDays_WorkoutTemplates_WorkoutTemplateId",
                        column: x => x.WorkoutTemplateId,
                        principalTable: "WorkoutTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserId_Date",
                table: "Activities",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledDays_WorkoutTemplateId",
                table: "ScheduledDays",
                column: "WorkoutTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "ConsistencySettings");

            migrationBuilder.DropTable(
                name: "ScheduledDays");
        }
    }
}
