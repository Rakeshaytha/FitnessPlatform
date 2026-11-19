using System;
using System.Collections.Generic;
using System.Text;

namespace FitnessPlatform.Application.WorkoutPlans;

public static class WorkoutPlanReviewWorkflowValidator
{
    public static bool CanSubmitForReview(
     string reviewStatus,
     bool isActive)
    {
        return (reviewStatus == "Draft" ||
                reviewStatus == "Rejected") && isActive;
    }

    public static bool CanApprove(
        string reviewStatus,
        bool isActive)
    {
        return reviewStatus == "In Review" && isActive;
    }

    public static bool CanReject(
        string reviewStatus)
    {
        return reviewStatus == "In Review";
    }


    public static bool CanEdit(
        string reviewStatus,
        bool isActive)
    {
        return (reviewStatus == "Draft" ||
                reviewStatus == "Rejected") && isActive;
    }

}

