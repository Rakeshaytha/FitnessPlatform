using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitnessPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPreferences_UserProfileId",
                table: "WorkoutPreferences",
                column: "UserProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DietaryPreferences_UserProfileId",
                table: "DietaryPreferences",
                column: "UserProfileId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DietaryPreferences_UserProfiles_UserProfileId",
                table: "DietaryPreferences",
                column: "UserProfileId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutPreferences_UserProfiles_UserProfileId",
                table: "WorkoutPreferences",
                column: "UserProfileId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DietaryPreferences_UserProfiles_UserProfileId",
                table: "DietaryPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutPreferences_UserProfiles_UserProfileId",
                table: "WorkoutPreferences");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutPreferences_UserProfileId",
                table: "WorkoutPreferences");

            migrationBuilder.DropIndex(
                name: "IX_DietaryPreferences_UserProfileId",
                table: "DietaryPreferences");
        }
    }
}
