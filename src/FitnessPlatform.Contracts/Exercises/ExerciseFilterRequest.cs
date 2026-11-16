using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.Exercises;

public class ExerciseFilterRequest
{
    public string? MuscleGroup { get; set; }

    public string? Equipment { get; set; }

    public string? DifficultyLevel { get; set; }

    public string? TrainingLocation { get; set; }

    public string? Search { get; set; }

    public bool? ActiveOnly { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
    public string? ReviewStatus { get; set; }
}
