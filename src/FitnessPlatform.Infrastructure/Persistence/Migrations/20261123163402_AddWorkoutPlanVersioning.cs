using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitnessPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutPlanVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLatestVersion",
                table: "WorkoutPlans",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousVersionId",
                table: "WorkoutPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VersionGroupId",
                table: "WorkoutPlans",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "VersionNumber",
                table: "WorkoutPlans",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Initialize versioning for existing workout plans.
            // Each existing plan becomes version 1 of its own version group.
            migrationBuilder.Sql(
                """
    UPDATE [WorkoutPlans]
    SET [VersionGroupId] = [Id]
    WHERE [VersionGroupId] = '00000000-0000-0000-0000-000000000000';
    """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPlans_PreviousVersionId",
                            table: "WorkoutPlans",
                column: "PreviousVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPlans_VersionGroupId_VersionNumber",
                table: "WorkoutPlans",
                columns: new[] { "VersionGroupId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkoutPlans_PreviousVersionId",
                table: "WorkoutPlans");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutPlans_VersionGroupId_VersionNumber",
                table: "WorkoutPlans");

            migrationBuilder.DropColumn(
                name: "IsLatestVersion",
                table: "WorkoutPlans");

            migrationBuilder.DropColumn(
                name: "PreviousVersionId",
                table: "WorkoutPlans");

            migrationBuilder.DropColumn(
                name: "VersionGroupId",
                table: "WorkoutPlans");

            migrationBuilder.DropColumn(
                name: "VersionNumber",
                table: "WorkoutPlans");
        }
    }
}
