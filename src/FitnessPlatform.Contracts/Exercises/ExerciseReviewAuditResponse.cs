using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.Exercises;

public sealed class ExerciseReviewAuditResponse
{
    public Guid Id { get; set; }

    public Guid ExerciseId { get; set; }

    public string PreviousStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string ActionSource { get; set; } = string.Empty;

    public string? PerformedByUserId { get; set; }

    public DateTime PerformedAtUtc { get; set; }
}
