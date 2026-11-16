using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Contracts.FitnessGoals;

public class FitnessGoalResponse
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}