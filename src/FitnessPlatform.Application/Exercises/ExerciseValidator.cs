using System;
using System.Collections.Generic;
using System.Text;
using FitnessPlatform.Contracts.Exercises;

namespace FitnessPlatform.Application.Exercises;

public static class ExerciseValidator
{
    public static string? Validate(SaveExerciseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) ||
            string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.MuscleGroup) ||
            string.IsNullOrWhiteSpace(request.Equipment) ||
            string.IsNullOrWhiteSpace(request.DifficultyLevel) ||
            string.IsNullOrWhiteSpace(request.TrainingLocation))
        {
            return "Required exercise fields are missing.";
        }

        var code = request.Code.Trim().ToUpperInvariant();

        if (code.Length > 50 ||
            request.Name.Trim().Length > 200 ||
            request.Description.Length > 2000 ||
            request.MuscleGroup.Trim().Length > 100 ||
            request.Equipment.Trim().Length > 100 ||
            request.DifficultyLevel.Trim().Length > 50 ||
            request.TrainingLocation.Trim().Length > 50 ||
            request.Instructions.Length > 4000 ||
            request.VideoUrl?.Length > 1000 ||
            request.GifUrl?.Length > 1000)
        {
            return "One or more fields exceed their maximum length.";
        }

        return null;
    }

    public static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }
}
