using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Domain.Entities;

public class MemberWorkoutPlanAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Member receiving the workout plan
    public Guid UserProfileId { get; set; }

    // References the exact workout plan version
    public Guid WorkoutPlanId { get; set; }

    // Assignment lifecycle
    public string Status { get; set; } = "Active";

    // Date the assignment becomes effective
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    // Optional planned completion date
    public DateOnly? EndDate { get; set; }

    // Date when the assignment was created
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;

    // Administrator who created the assignment
    public Guid AssignedByUserId { get; set; }

    // Optional reason for assignment or reassignment
    public string? Notes { get; set; }

    // Assignment history
    public DateTime? DeactivatedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public UserProfile UserProfile { get; set; } = null!;

    public WorkoutPlan WorkoutPlan { get; set; } = null!;
}