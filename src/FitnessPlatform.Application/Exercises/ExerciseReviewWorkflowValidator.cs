namespace FitnessPlatform.Application.Exercises;

public static class ExerciseReviewWorkflowValidator
{
    public static bool CanSubmitForReview(
        string reviewStatus)
    {
        return reviewStatus == "Draft";
    }

    public static bool CanApprove(
        string reviewStatus,
        bool isActive)
    {
        return reviewStatus == "In Review" && isActive;
    }

    public static bool CanRetire(
        string reviewStatus)
    {
        return reviewStatus == "Approved";
    }

    public static bool CanBulkSubmitForReview(
        string reviewStatus,
        bool isActive)
    {
        return reviewStatus == "Draft" && isActive;
    }

    public static bool CanBulkApprove(
        string reviewStatus,
        bool isActive)
    {
        return reviewStatus == "In Review" && isActive;
    }
}