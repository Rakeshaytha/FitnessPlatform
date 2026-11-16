using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Domain.Entities;

public sealed class ExerciseReviewAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ExerciseId { get; set; }

    public string PreviousStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string ActionSource { get; set; } = string.Empty;

    public string? PerformedByUserId { get; set; }

    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;

    public Exercise Exercise { get; set; } = null!;
}
