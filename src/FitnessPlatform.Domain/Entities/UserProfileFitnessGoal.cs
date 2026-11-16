using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Domain.Entities;

public class UserProfileFitnessGoal
{
    public Guid UserProfileId { get; set; }

    public int FitnessGoalId { get; set; }

    public UserProfile UserProfile { get; set; } = null!;

    public FitnessGoal FitnessGoal { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
