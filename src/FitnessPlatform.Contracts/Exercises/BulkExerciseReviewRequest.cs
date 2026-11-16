using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.Exercises;

public sealed class BulkExerciseReviewRequest
{
    public List<Guid> ExerciseIds { get; set; } = new();
}
