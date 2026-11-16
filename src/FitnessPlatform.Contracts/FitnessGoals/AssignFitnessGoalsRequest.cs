using System;
using System.Collections.Generic;
using System.Text;
namespace FitnessPlatform.Contracts.FitnessGoals;

public class AssignFitnessGoalsRequest
{
    public List<int> FitnessGoalIds { get; set; } = new();
}
