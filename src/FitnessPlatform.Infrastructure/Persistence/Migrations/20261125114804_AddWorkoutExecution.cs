using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitnessPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkoutSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberWorkoutPlanAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkoutPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkoutPlanDayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkoutPlanName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkoutPlanVersionNumber = table.Column<int>(type: "int", nullable: false),
                    WorkoutDayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    WorkoutDayNumber = table.Column<int>(type: "int", nullable: false),
                    WorkoutDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "InProgress"),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PausedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalPausedSeconds = table.Column<int>(type: "int", nullable: false),
                    TotalDurationSeconds = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutSessions", x => x.Id);
                    table.CheckConstraint("CK_WorkoutSessions_CancelledAtUtc", "[CancelledAtUtc] IS NULL OR [CancelledAtUtc] >= [StartedAtUtc]");
                    table.CheckConstraint("CK_WorkoutSessions_CompletedAtUtc", "[CompletedAtUtc] IS NULL OR [CompletedAtUtc] >= [StartedAtUtc]");
                    table.CheckConstraint("CK_WorkoutSessions_DayNumber", "[WorkoutDayNumber] > 0");
                    table.CheckConstraint("CK_WorkoutSessions_Lifecycle", "(([Status] = 'InProgress' AND [PausedAtUtc] IS NULL AND [CompletedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL AND [TotalDurationSeconds] IS NULL) OR ([Status] = 'Paused' AND [PausedAtUtc] IS NOT NULL AND [CompletedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL AND [TotalDurationSeconds] IS NULL) OR ([Status] = 'Completed' AND [PausedAtUtc] IS NULL AND [CompletedAtUtc] IS NOT NULL AND [CancelledAtUtc] IS NULL AND [TotalDurationSeconds] IS NOT NULL) OR ([Status] = 'Cancelled' AND [PausedAtUtc] IS NULL AND [CompletedAtUtc] IS NULL AND [CancelledAtUtc] IS NOT NULL AND [TotalDurationSeconds] IS NOT NULL))");
                    table.CheckConstraint("CK_WorkoutSessions_PausedAtUtc", "[PausedAtUtc] IS NULL OR [PausedAtUtc] >= [StartedAtUtc]");
                    table.CheckConstraint("CK_WorkoutSessions_PlanVersion", "[WorkoutPlanVersionNumber] > 0");
                    table.CheckConstraint("CK_WorkoutSessions_Status", "[Status] IN ('InProgress', 'Paused', 'Completed', 'Cancelled')");
                    table.CheckConstraint("CK_WorkoutSessions_TotalDurationSeconds", "[TotalDurationSeconds] IS NULL OR [TotalDurationSeconds] >= 0");
                    table.CheckConstraint("CK_WorkoutSessions_TotalPausedSeconds", "[TotalPausedSeconds] >= 0");
                    table.ForeignKey(
                        name: "FK_WorkoutSessions_MemberWorkoutPlanAssignments_MemberWorkoutPlanAssignmentId",
                        column: x => x.MemberWorkoutPlanAssignmentId,
                        principalTable: "MemberWorkoutPlanAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkoutSessions_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkoutSessions_WorkoutPlanDays_WorkoutPlanDayId",
                        column: x => x.WorkoutPlanDayId,
                        principalTable: "WorkoutPlanDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkoutSessions_WorkoutPlans_WorkoutPlanId",
                        column: x => x.WorkoutPlanId,
                        principalTable: "WorkoutPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutSessionExercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkoutSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkoutPlanExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExerciseCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExerciseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MuscleGroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Equipment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExerciseInstructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ExerciseOrder = table.Column<int>(type: "int", nullable: false),
                    PlannedSets = table.Column<int>(type: "int", nullable: false),
                    PlannedReps = table.Column<int>(type: "int", nullable: false),
                    PlannedRestSeconds = table.Column<int>(type: "int", nullable: false),
                    PlannedDurationSeconds = table.Column<int>(type: "int", nullable: true),
                    PlannedNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SkippedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SkipReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutSessionExercises", x => x.Id);
                    table.CheckConstraint("CK_WorkoutSessionExercises_CompletedAtUtc", "[CompletedAtUtc] IS NULL OR ([StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [StartedAtUtc])");
                    table.CheckConstraint("CK_WorkoutSessionExercises_ExerciseOrder", "[ExerciseOrder] > 0");
                    table.CheckConstraint("CK_WorkoutSessionExercises_Lifecycle", "(([Status] = 'Pending' AND [StartedAtUtc] IS NULL AND [CompletedAtUtc] IS NULL AND [SkippedAtUtc] IS NULL) OR ([Status] = 'InProgress' AND [StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] IS NULL AND [SkippedAtUtc] IS NULL) OR ([Status] = 'Completed' AND [StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [SkippedAtUtc] IS NULL) OR ([Status] = 'Skipped' AND [CompletedAtUtc] IS NULL AND [SkippedAtUtc] IS NOT NULL))");
                    table.CheckConstraint("CK_WorkoutSessionExercises_PlannedDurationSeconds", "[PlannedDurationSeconds] IS NULL OR [PlannedDurationSeconds] >= 0");
                    table.CheckConstraint("CK_WorkoutSessionExercises_PlannedReps", "[PlannedReps] >= 0");
                    table.CheckConstraint("CK_WorkoutSessionExercises_PlannedRestSeconds", "[PlannedRestSeconds] >= 0");
                    table.CheckConstraint("CK_WorkoutSessionExercises_PlannedSets", "[PlannedSets] > 0");
                    table.CheckConstraint("CK_WorkoutSessionExercises_SkippedAtUtc", "[SkippedAtUtc] IS NULL OR [StartedAtUtc] IS NULL OR [SkippedAtUtc] >= [StartedAtUtc]");
                    table.CheckConstraint("CK_WorkoutSessionExercises_Status", "[Status] IN ('Pending', 'InProgress', 'Completed', 'Skipped')");
                    table.ForeignKey(
                        name: "FK_WorkoutSessionExercises_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkoutSessionExercises_WorkoutPlanExercises_WorkoutPlanExerciseId",
                        column: x => x.WorkoutPlanExerciseId,
                        principalTable: "WorkoutPlanExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkoutSessionExercises_WorkoutSessions_WorkoutSessionId",
                        column: x => x.WorkoutSessionId,
                        principalTable: "WorkoutSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutSessionSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkoutSessionExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SetNumber = table.Column<int>(type: "int", nullable: false),
                    SetType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Working"),
                    ActualReps = table.Column<int>(type: "int", nullable: true),
                    ActualWeightKg = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: true),
                    ActualDurationSeconds = table.Column<int>(type: "int", nullable: true),
                    ActualDistanceMeters = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    ActualRestSeconds = table.Column<int>(type: "int", nullable: true),
                    Rpe = table.Column<decimal>(type: "decimal(3,1)", precision: 3, scale: 1, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SkippedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutSessionSets", x => x.Id);
                    table.CheckConstraint("CK_WorkoutSessionSets_ActualDistanceMeters", "[ActualDistanceMeters] IS NULL OR [ActualDistanceMeters] >= 0");
                    table.CheckConstraint("CK_WorkoutSessionSets_ActualDurationSeconds", "[ActualDurationSeconds] IS NULL OR [ActualDurationSeconds] >= 0");
                    table.CheckConstraint("CK_WorkoutSessionSets_ActualReps", "[ActualReps] IS NULL OR [ActualReps] >= 0");
                    table.CheckConstraint("CK_WorkoutSessionSets_ActualRestSeconds", "[ActualRestSeconds] IS NULL OR [ActualRestSeconds] >= 0");
                    table.CheckConstraint("CK_WorkoutSessionSets_ActualWeightKg", "[ActualWeightKg] IS NULL OR [ActualWeightKg] >= 0");
                    table.CheckConstraint("CK_WorkoutSessionSets_CompletedPerformance", "[Status] <> 'Completed' OR (COALESCE([ActualReps], 0) > 0 OR COALESCE([ActualDurationSeconds], 0) > 0 OR COALESCE([ActualDistanceMeters], 0) > 0)");
                    table.CheckConstraint("CK_WorkoutSessionSets_Lifecycle", "(([Status] = 'Pending' AND [CompletedAtUtc] IS NULL AND [SkippedAtUtc] IS NULL) OR ([Status] = 'Completed' AND [CompletedAtUtc] IS NOT NULL AND [SkippedAtUtc] IS NULL) OR ([Status] = 'Skipped' AND [CompletedAtUtc] IS NULL AND [SkippedAtUtc] IS NOT NULL))");
                    table.CheckConstraint("CK_WorkoutSessionSets_Rpe", "[Rpe] IS NULL OR ([Rpe] >= 1 AND [Rpe] <= 10)");
                    table.CheckConstraint("CK_WorkoutSessionSets_SetNumber", "[SetNumber] > 0");
                    table.CheckConstraint("CK_WorkoutSessionSets_SetType", "[SetType] IN ('Warmup', 'Working', 'Drop', 'Failure', 'Cooldown')");
                    table.CheckConstraint("CK_WorkoutSessionSets_Status", "[Status] IN ('Pending', 'Completed', 'Skipped')");
                    table.ForeignKey(
                        name: "FK_WorkoutSessionSets_WorkoutSessionExercises_WorkoutSessionExerciseId",
                        column: x => x.WorkoutSessionExerciseId,
                        principalTable: "WorkoutSessionExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessionExercises_ExerciseId",
                table: "WorkoutSessionExercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessionExercises_WorkoutPlanExerciseId",
                table: "WorkoutSessionExercises",
                column: "WorkoutPlanExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessionExercises_WorkoutSessionId_ExerciseOrder",
                table: "WorkoutSessionExercises",
                columns: new[] { "WorkoutSessionId", "ExerciseOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessionExercises_WorkoutSessionId_WorkoutPlanExerciseId",
                table: "WorkoutSessionExercises",
                columns: new[] { "WorkoutSessionId", "WorkoutPlanExerciseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_MemberWorkoutPlanAssignmentId",
                table: "WorkoutSessions",
                column: "MemberWorkoutPlanAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_UserProfileId_WorkoutDate",
                table: "WorkoutSessions",
                columns: new[] { "UserProfileId", "WorkoutDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_WorkoutPlanDayId",
                table: "WorkoutSessions",
                column: "WorkoutPlanDayId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_WorkoutPlanId",
                table: "WorkoutSessions",
                column: "WorkoutPlanId");

            migrationBuilder.CreateIndex(
                name: "UX_WorkoutSessions_UnfinishedUserProfile",
                table: "WorkoutSessions",
                column: "UserProfileId",
                unique: true,
                filter: "[Status] IN ('InProgress', 'Paused')");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessionSets_WorkoutSessionExerciseId_SetNumber",
                table: "WorkoutSessionSets",
                columns: new[] { "WorkoutSessionExerciseId", "SetNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkoutSessionSets");

            migrationBuilder.DropTable(
                name: "WorkoutSessionExercises");

            migrationBuilder.DropTable(
                name: "WorkoutSessions");
        }
    }
}
