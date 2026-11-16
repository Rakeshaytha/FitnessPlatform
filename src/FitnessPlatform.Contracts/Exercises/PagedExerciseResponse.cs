using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.Exercises;

public class PagedExerciseResponse
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public List<ExerciseResponse> Items { get; set; } = new();
}
