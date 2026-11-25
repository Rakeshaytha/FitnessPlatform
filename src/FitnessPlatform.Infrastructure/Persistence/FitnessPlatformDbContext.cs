using System;
using System.Collections.Generic;
using System.Text;
using FitnessPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using FitnessPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using FitnessPlatform.Domain.Constants;
namespace FitnessPlatform.Infrastructure.Persistence;

public class FitnessPlatformDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public FitnessPlatformDbContext(
        DbContextOptions<FitnessPlatformDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserProfile> UserProfiles { get; set; }

    public DbSet<FitnessGoal> FitnessGoals { get; set; }

    public DbSet<WorkoutPreference> WorkoutPreferences { get; set; }

    public DbSet<DietaryPreference> DietaryPreferences { get; set; }

    public DbSet<UserProfileFitnessGoal> UserProfileFitnessGoals { get; set; }

    public DbSet<Exercise> Exercises { get; set; }

    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();

    public DbSet<MemberWorkoutPlanAssignment> MemberWorkoutPlanAssignments => Set<MemberWorkoutPlanAssignment>();

    public DbSet<WorkoutPlanDay> WorkoutPlanDays => Set<WorkoutPlanDay>();

    public DbSet<WorkoutPlanExercise> WorkoutPlanExercises => Set<WorkoutPlanExercise>();

    public DbSet<ExerciseReviewAudit> ExerciseReviewAudits => Set<ExerciseReviewAudit>();
    public DbSet<WorkoutSession> WorkoutSessions =>
    Set<WorkoutSession>();

    public DbSet<WorkoutSessionExercise> WorkoutSessionExercises =>
        Set<WorkoutSessionExercise>();

    public DbSet<WorkoutSessionSet> WorkoutSessionSets =>
        Set<WorkoutSessionSet>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.LastName)
                .HasMaxLength(100);

            entity.Property(x => x.HeightCm)
                .HasPrecision(5, 2);

            entity.Property(x => x.WeightKg)
                .HasPrecision(5, 2);

            entity.Property(x => x.CountryCode)
                .HasMaxLength(2);
        });

        modelBuilder.Entity<FitnessGoal>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Code)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(x => x.Code).IsUnique();

            entity.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();
        });

        modelBuilder.Entity<WorkoutPreference>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.TrainingLocation)
                .HasMaxLength(30);

            entity.Property(x => x.ExperienceLevel)
                .HasMaxLength(30);

            entity.HasOne<UserProfile>()
                .WithOne()
                .HasForeignKey<WorkoutPreference>(x => x.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DietaryPreference>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.DietType)
                .HasMaxLength(50);

            entity.Property(x => x.PreferredCuisine)
                .HasMaxLength(100);

            entity.HasOne<UserProfile>()
                .WithOne()
                .HasForeignKey<DietaryPreference>(x => x.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);

        });

        modelBuilder.Entity<UserProfileFitnessGoal>(entity =>
        {
            entity.HasKey(x => new
            {
                x.UserProfileId,
                x.FitnessGoalId
            });

            entity.HasOne(x => x.UserProfile)
                .WithMany(x => x.FitnessGoals)
                .HasForeignKey(x => x.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.FitnessGoal)
                .WithMany(x => x.UserProfiles)
                .HasForeignKey(x => x.FitnessGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();
        });

        modelBuilder.Entity<Exercise>(entity =>
        {
            entity.ToTable("Exercises");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Code)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(2000);

            entity.Property(x => x.MuscleGroup)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Equipment)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.DifficultyLevel)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.TrainingLocation)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Instructions)
                .HasMaxLength(4000);

            entity.Property(x => x.VideoUrl)
                .HasMaxLength(1000);

            entity.Property(x => x.GifUrl)
                .HasMaxLength(1000);

            entity.Property(x => x.ReviewStatus)
                .HasMaxLength(30)
                .IsRequired()
                .HasDefaultValue("Draft");

            entity.Property(x => x.IsActive)
                .HasDefaultValue(true);




            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();
        });

        modelBuilder.Entity<ExerciseReviewAudit>(entity =>
        {
            entity.ToTable("ExerciseReviewAudits");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.PreviousStatus)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.NewStatus)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.Action)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.ActionSource)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.PerformedByUserId)
                .HasMaxLength(100);

            entity.Property(x => x.PerformedAtUtc)
                .IsRequired();

            entity.HasOne(x => x.Exercise)
                .WithMany()
                .HasForeignKey(x => x.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ExerciseId);

            entity.HasIndex(x => x.PerformedAtUtc);
        });


        modelBuilder.Entity<WorkoutPlan>(entity =>
        {
            entity.ToTable("WorkoutPlans");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(2000);

            entity.Property(x => x.GoalCode)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.ExperienceLevel)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.TrainingLocation)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.ReviewStatus)
                .HasMaxLength(30)
                .IsRequired()
                .HasDefaultValue("Draft");

            entity.Property(x => x.RejectionReason)
                .HasMaxLength(1000)
                .IsRequired(false);

            entity.Property(x => x.IsActive)
                .HasDefaultValue(true);

            // Workout Plan Versioning

            entity.Property(x => x.VersionNumber)
                .IsRequired()
                .HasDefaultValue(1);

            entity.Property(x => x.VersionGroupId)
                .IsRequired();

            entity.Property(x => x.PreviousVersionId)
                .IsRequired(false);

            entity.Property(x => x.IsLatestVersion)
                .IsRequired()
                .HasDefaultValue(true);

            entity.HasIndex(x => new
            {
                x.VersionGroupId,
                x.VersionNumber
            })
            .IsUnique();

            entity.HasIndex(x => x.PreviousVersionId);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.GoalCode,
                x.ExperienceLevel,
                x.TrainingLocation
            });
        });

        modelBuilder.Entity<WorkoutPlanDay>(entity =>
        {
            entity.ToTable("WorkoutPlanDays");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(1000);

            entity.HasOne(x => x.WorkoutPlan)
                .WithMany()
                .HasForeignKey(x => x.WorkoutPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new
            {
                x.WorkoutPlanId,
                x.DayNumber
            }).IsUnique();
        });

        modelBuilder.Entity<WorkoutPlanExercise>(entity =>
        {
            entity.ToTable("WorkoutPlanExercises");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Notes)
                .HasMaxLength(1000);

            entity.HasOne(x => x.WorkoutPlanDay)
                .WithMany(x => x.Exercises)
                .HasForeignKey(x => x.WorkoutPlanDayId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Exercise)
                .WithMany()
                .HasForeignKey(x => x.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.WorkoutPlanDayId,
                x.ExerciseOrder
            }).IsUnique();

            entity.HasIndex(x => x.ExerciseId);
        });

        // Member Workout Plan Assignment Configuration
        modelBuilder.Entity<MemberWorkoutPlanAssignment>(entity =>
        {
            entity.ToTable("MemberWorkoutPlanAssignments");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserProfileId)
                .IsRequired();

            entity.Property(x => x.WorkoutPlanId)
                .IsRequired();

            entity.Property(x => x.AssignedByUserId)
                .IsRequired();

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(30)
                .HasDefaultValue("Active");

            entity.Property(x => x.StartDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(x => x.EndDate)
                .HasColumnType("date");

            entity.Property(x => x.Notes)
                .HasMaxLength(1000);

            entity.Property(x => x.AssignedAtUtc)
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            // Relationship with member profile
            entity.HasOne(x => x.UserProfile)
                .WithMany()
                .HasForeignKey(x => x.UserProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with exact workout plan version
            entity.HasOne(x => x.WorkoutPlan)
                .WithMany()
                .HasForeignKey(x => x.WorkoutPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            // Only one active assignment per member
            entity.HasIndex(x => x.UserProfileId)
                .IsUnique()
                .HasFilter("[Status] = 'Active'")
                .HasDatabaseName(
                    "UX_MemberWorkoutPlanAssignments_ActiveUserProfile");

            // Optimize workout plan assignment searches
            entity.HasIndex(x => x.WorkoutPlanId);

            // Optimize member assignment history
            entity.HasIndex(x => new
            {
                x.UserProfileId,
                x.AssignedAtUtc
            });

            // Database-level validation
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_MemberWorkoutPlanAssignments_Status",
                    "[Status] IN ('Active', 'Inactive', 'Completed', 'Cancelled')");

                table.HasCheckConstraint(
                    "CK_MemberWorkoutPlanAssignments_DateRange",
                    "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
            });
        });

        // Workout Session Configuration
        modelBuilder.Entity<WorkoutSession>(entity =>
        {
            entity.ToTable("WorkoutSessions");

            entity.HasKey(x => x.Id);

            // Historical workout plan snapshot
            entity.Property(x => x.WorkoutPlanName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.WorkoutPlanVersionNumber)
                .IsRequired();

            // Historical workout day snapshot
            entity.Property(x => x.WorkoutDayName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.WorkoutDayNumber)
                .IsRequired();

            // Local workout date
            entity.Property(x => x.WorkoutDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(x => x.TimeZoneId)
                .HasMaxLength(100)
                .IsRequired();

            // Session lifecycle
            entity.Property(x => x.Status)
                .HasMaxLength(30)
                .IsRequired()
                .HasDefaultValue(WorkoutSessionStatuses.InProgress);

            // Session timestamps
            entity.Property(x => x.StartedAtUtc)
                .IsRequired();

            entity.Property(x => x.TotalPausedSeconds)
                .IsRequired();

            entity.Property(x => x.Notes)
                .HasMaxLength(1000);

            // Audit information
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            // Optimistic concurrency protection
            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            // Relationship with member profile
            entity.HasOne(x => x.UserProfile)
                .WithMany()
                .HasForeignKey(x => x.UserProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with member workout assignment
            entity.HasOne(x => x.MemberWorkoutPlanAssignment)
                .WithMany()
                .HasForeignKey(x => x.MemberWorkoutPlanAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with exact workout plan version
            entity.HasOne(x => x.WorkoutPlan)
                .WithMany()
                .HasForeignKey(x => x.WorkoutPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with workout plan day
            entity.HasOne(x => x.WorkoutPlanDay)
                .WithMany()
                .HasForeignKey(x => x.WorkoutPlanDayId)
                .OnDelete(DeleteBehavior.Restrict);

            // Only one unfinished workout session per member
            entity.HasIndex(x => x.UserProfileId)
                .IsUnique()
                .HasFilter("[Status] IN ('InProgress', 'Paused')")
                .HasDatabaseName(
                    "UX_WorkoutSessions_UnfinishedUserProfile");

            // Optimize member workout history
            entity.HasIndex(x => new
            {
                x.UserProfileId,
                x.WorkoutDate
            });

            // Optimize assignment-based reporting
            entity.HasIndex(x => x.MemberWorkoutPlanAssignmentId);

            // Optimize workout plan reporting
            entity.HasIndex(x => x.WorkoutPlanId);

            // Optimize workout day reporting
            entity.HasIndex(x => x.WorkoutPlanDayId);

            // Database-level validation
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_WorkoutSessions_Status",
                    "[Status] IN ('InProgress', 'Paused', 'Completed', 'Cancelled')");

                table.HasCheckConstraint(
                    "CK_WorkoutSessions_PlanVersion",
                    "[WorkoutPlanVersionNumber] > 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessions_DayNumber",
                    "[WorkoutDayNumber] > 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessions_TotalPausedSeconds",
                    "[TotalPausedSeconds] >= 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessions_TotalDurationSeconds",
                    "[TotalDurationSeconds] IS NULL OR [TotalDurationSeconds] >= 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessions_CompletedAtUtc",
                    "[CompletedAtUtc] IS NULL OR [CompletedAtUtc] >= [StartedAtUtc]");

                table.HasCheckConstraint(
                    "CK_WorkoutSessions_CancelledAtUtc",
                    "[CancelledAtUtc] IS NULL OR [CancelledAtUtc] >= [StartedAtUtc]");

                table.HasCheckConstraint(
                    "CK_WorkoutSessions_PausedAtUtc",
                    "[PausedAtUtc] IS NULL OR [PausedAtUtc] >= [StartedAtUtc]");

                table.HasCheckConstraint(
                    "CK_WorkoutSessions_Lifecycle",
                    "(" +
                    "([Status] = 'InProgress' AND " +
                    "[PausedAtUtc] IS NULL AND " +
                    "[CompletedAtUtc] IS NULL AND " +
                    "[CancelledAtUtc] IS NULL AND " +
                    "[TotalDurationSeconds] IS NULL) OR " +

                    "([Status] = 'Paused' AND " +
                    "[PausedAtUtc] IS NOT NULL AND " +
                    "[CompletedAtUtc] IS NULL AND " +
                    "[CancelledAtUtc] IS NULL AND " +
                    "[TotalDurationSeconds] IS NULL) OR " +

                    "([Status] = 'Completed' AND " +
                    "[PausedAtUtc] IS NULL AND " +
                    "[CompletedAtUtc] IS NOT NULL AND " +
                    "[CancelledAtUtc] IS NULL AND " +
                    "[TotalDurationSeconds] IS NOT NULL) OR " +

                    "([Status] = 'Cancelled' AND " +
                    "[PausedAtUtc] IS NULL AND " +
                    "[CompletedAtUtc] IS NULL AND " +
                    "[CancelledAtUtc] IS NOT NULL AND " +
                    "[TotalDurationSeconds] IS NOT NULL)" +
                    ")");
            });
        });

        // Workout Session Exercise Configuration
        modelBuilder.Entity<WorkoutSessionExercise>(entity =>
        {
            entity.ToTable("WorkoutSessionExercises");

            entity.HasKey(x => x.Id);

            // Historical exercise snapshot
            entity.Property(x => x.ExerciseCode)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.ExerciseName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.MuscleGroup)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Equipment)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.ExerciseInstructions)
                .HasMaxLength(4000)
                .IsRequired();

            // Planned exercise parameters
            entity.Property(x => x.PlannedNotes)
                .HasMaxLength(1000);

            // Exercise lifecycle
            entity.Property(x => x.Status)
                .HasMaxLength(30)
                .IsRequired()
                .HasDefaultValue(
                    WorkoutSessionExerciseStatuses.Pending);

            // Member feedback
            entity.Property(x => x.Notes)
                .HasMaxLength(1000);

            entity.Property(x => x.SkipReason)
                .HasMaxLength(1000);

            // Audit information
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            // Relationship with parent workout session
            entity.HasOne(x => x.WorkoutSession)
                .WithMany(x => x.Exercises)
                .HasForeignKey(x => x.WorkoutSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with original workout plan exercise
            entity.HasOne(x => x.WorkoutPlanExercise)
                .WithMany()
                .HasForeignKey(x => x.WorkoutPlanExerciseId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Exercise Master
            entity.HasOne(x => x.Exercise)
                .WithMany()
                .HasForeignKey(x => x.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);

            // Prevent duplicate exercise sequence
            entity.HasIndex(x => new
            {
                x.WorkoutSessionId,
                x.ExerciseOrder
            })
            .IsUnique();

            // Prevent duplicate original exercise assignment
            entity.HasIndex(x => new
            {
                x.WorkoutSessionId,
                x.WorkoutPlanExerciseId
            })
            .IsUnique();

            // Optimize exercise performance history
            entity.HasIndex(x => x.ExerciseId);

            entity.HasIndex(x => x.WorkoutPlanExerciseId);

            // Database-level validation
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_WorkoutSessionExercises_Status",
                    "[Status] IN ('Pending', 'InProgress', 'Completed', 'Skipped')");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionExercises_ExerciseOrder",
                    "[ExerciseOrder] > 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionExercises_PlannedSets",
                    "[PlannedSets] > 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionExercises_PlannedReps",
                    "[PlannedReps] >= 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionExercises_PlannedRestSeconds",
                    "[PlannedRestSeconds] >= 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionExercises_PlannedDurationSeconds",
                    "[PlannedDurationSeconds] IS NULL OR [PlannedDurationSeconds] >= 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionExercises_CompletedAtUtc",
                    "[CompletedAtUtc] IS NULL OR " +
                    "([StartedAtUtc] IS NOT NULL AND " +
                    "[CompletedAtUtc] >= [StartedAtUtc])");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionExercises_SkippedAtUtc",
                    "[SkippedAtUtc] IS NULL OR " +
                    "[StartedAtUtc] IS NULL OR " +
                    "[SkippedAtUtc] >= [StartedAtUtc]");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionExercises_Lifecycle",
                    "(" +
                    "([Status] = 'Pending' AND " +
                    "[StartedAtUtc] IS NULL AND " +
                    "[CompletedAtUtc] IS NULL AND " +
                    "[SkippedAtUtc] IS NULL) OR " +

                    "([Status] = 'InProgress' AND " +
                    "[StartedAtUtc] IS NOT NULL AND " +
                    "[CompletedAtUtc] IS NULL AND " +
                    "[SkippedAtUtc] IS NULL) OR " +

                    "([Status] = 'Completed' AND " +
                    "[StartedAtUtc] IS NOT NULL AND " +
                    "[CompletedAtUtc] IS NOT NULL AND " +
                    "[SkippedAtUtc] IS NULL) OR " +

                    "([Status] = 'Skipped' AND " +
                    "[CompletedAtUtc] IS NULL AND " +
                    "[SkippedAtUtc] IS NOT NULL)" +
                    ")");
            });
        });

        // Workout Session Set Configuration
        modelBuilder.Entity<WorkoutSessionSet>(entity =>
        {
            entity.ToTable("WorkoutSessionSets");

            entity.HasKey(x => x.Id);

            // Set classification
            entity.Property(x => x.SetType)
                .HasMaxLength(30)
                .IsRequired()
                .HasDefaultValue(WorkoutSessionSetTypes.Working);

            // Performance measurements
            entity.Property(x => x.ActualWeightKg)
                .HasPrecision(10, 3);

            entity.Property(x => x.ActualDistanceMeters)
                .HasPrecision(12, 2);

            entity.Property(x => x.Rpe)
                .HasPrecision(3, 1);

            // Set lifecycle
            entity.Property(x => x.Status)
                .HasMaxLength(30)
                .IsRequired()
                .HasDefaultValue(WorkoutSessionSetStatuses.Pending);

            // Member feedback
            entity.Property(x => x.Notes)
                .HasMaxLength(1000);

            // Audit information
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            // Relationship with parent session exercise
            entity.HasOne(x => x.WorkoutSessionExercise)
                .WithMany(x => x.Sets)
                .HasForeignKey(x => x.WorkoutSessionExerciseId)
                .OnDelete(DeleteBehavior.Restrict);

            // Prevent duplicate set numbers within an exercise
            entity.HasIndex(x => new
            {
                x.WorkoutSessionExerciseId,
                x.SetNumber
            })
            .IsUnique();

            // Database-level validation
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_WorkoutSessionSets_Status",
                    "[Status] IN ('Pending', 'Completed', 'Skipped')");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionSets_SetType",
                    "[SetType] IN ('Warmup', 'Working', 'Drop', 'Failure', 'Cooldown')");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionSets_SetNumber",
                    "[SetNumber] > 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionSets_ActualReps",
                    "[ActualReps] IS NULL OR [ActualReps] >= 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionSets_ActualWeightKg",
                    "[ActualWeightKg] IS NULL OR [ActualWeightKg] >= 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionSets_ActualDurationSeconds",
                    "[ActualDurationSeconds] IS NULL OR [ActualDurationSeconds] >= 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionSets_ActualDistanceMeters",
                    "[ActualDistanceMeters] IS NULL OR [ActualDistanceMeters] >= 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionSets_ActualRestSeconds",
                    "[ActualRestSeconds] IS NULL OR [ActualRestSeconds] >= 0");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionSets_Rpe",
                    "[Rpe] IS NULL OR ([Rpe] >= 1 AND [Rpe] <= 10)");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionSets_Lifecycle",
                    "(" +
                    "([Status] = 'Pending' AND " +
                    "[CompletedAtUtc] IS NULL AND " +
                    "[SkippedAtUtc] IS NULL) OR " +

                    "([Status] = 'Completed' AND " +
                    "[CompletedAtUtc] IS NOT NULL AND " +
                    "[SkippedAtUtc] IS NULL) OR " +

                    "([Status] = 'Skipped' AND " +
                    "[CompletedAtUtc] IS NULL AND " +
                    "[SkippedAtUtc] IS NOT NULL)" +
                    ")");

                table.HasCheckConstraint(
                    "CK_WorkoutSessionSets_CompletedPerformance",
                    "[Status] <> 'Completed' OR " +
                    "(" +
                    "COALESCE([ActualReps], 0) > 0 OR " +
                    "COALESCE([ActualDurationSeconds], 0) > 0 OR " +
                    "COALESCE([ActualDistanceMeters], 0) > 0" +
                    ")");
            });
        });



    }


}