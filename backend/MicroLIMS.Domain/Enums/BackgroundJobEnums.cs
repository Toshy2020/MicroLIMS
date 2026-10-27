namespace MicroLIMS.Domain.Enums;

public enum BackgroundJobOutcome
{
    Running = 0,
    Succeeded = 1,
    Failed = 2,
    Skipped = 3
}

public enum BackgroundJobTrigger
{
    Schedule = 0,
    Manual = 1
}
