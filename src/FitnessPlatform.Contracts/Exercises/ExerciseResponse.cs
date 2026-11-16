using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.Exercises;

public class ExerciseResponse
{
    public Guid Id { get; set; }

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

    public bool IsActive { get; set; }

    public string ReviewStatus { get; set; } = string.Empty;
}
