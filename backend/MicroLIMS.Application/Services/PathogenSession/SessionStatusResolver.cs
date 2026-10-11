using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// The overall status line of a pathogen testing session. Pure.
public static class SessionStatusResolver
{
    public static (string Status, string Display) Resolve(
        int completedCount, int totalRequired, SampleStatus sampleStatus,
        bool tsbIncubating, bool tsbCompleted, int tsbApplicableTestCount)
    {
        string sessionStatus = "NOT_STARTED";
        string sessionStatusDisplay = "Not Started";

        if (completedCount == totalRequired && totalRequired > 0)
        {
            sessionStatus = sampleStatus == SampleStatus.Approved ? "COMPLETED" : "READY_FOR_REVIEW";
            sessionStatusDisplay = sampleStatus == SampleStatus.Approved ? "Completed & Approved" : "Ready for Technical Review";
        }
        else if (completedCount > 0)
        {
            sessionStatus = "RESULTS_IN_PROGRESS";
            sessionStatusDisplay = $"Results In Progress ({completedCount}/{totalRequired})";
        }
        else if (tsbIncubating)
        {
            sessionStatus = "TSB_INCUBATING";
            sessionStatusDisplay = "TSB Enrichment Incubating";
        }
        else if (tsbCompleted)
        {
            sessionStatus = "TSB_COMPLETED";
            sessionStatusDisplay = "TSB Complete, Downstream Testing";
        }
        else if (tsbApplicableTestCount == 0)
        {
            sessionStatus = "DOWNSTREAM_TESTING";
            sessionStatusDisplay = "Testing In Progress";
        }

        return (sessionStatus, sessionStatusDisplay);
    }
}
