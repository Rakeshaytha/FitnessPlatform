using System;

namespace FitnessPlatform.Domain.Entities;

public class Exercise
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string MuscleGroup { get; set; } = string.Empty;

    public string Equipment { get; set; } = string.Empty;

    public string DifficultyLevel { get; set; } = string.Empty;

    public string TrainingLocation { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public string? VideoUrl { get; set; }

    public string? GifUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public string ReviewStatus { get; set; } = "Draft";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}