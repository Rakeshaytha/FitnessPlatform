using System;
using System.Collections.Generic;
using System.Text;
using FitnessPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using FitnessPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

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

    public DbSet<ExerciseReviewAudit> ExerciseReviewAudits => Set<ExerciseReviewAudit>();
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
    }
}