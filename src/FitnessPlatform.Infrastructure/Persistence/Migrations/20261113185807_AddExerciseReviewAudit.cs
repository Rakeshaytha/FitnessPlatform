using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitnessPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseReviewAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseReviewAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActionSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PerformedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseReviewAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseReviewAudits_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseReviewAudits_ExerciseId",
                table: "ExerciseReviewAudits",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseReviewAudits_PerformedAtUtc",
                table: "ExerciseReviewAudits",
                column: "PerformedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseReviewAudits");
        }
    }
}
