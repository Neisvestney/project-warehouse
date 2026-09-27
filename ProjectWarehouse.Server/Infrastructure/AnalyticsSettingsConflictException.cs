namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>Someone else saved the analytics settings between this caller's read and write.</summary>
public class AnalyticsSettingsConflictException(Exception? inner = null)
    : Exception("Analytics settings were modified by another user.", inner), IExpectedFailure
{
    ErrorCode? IExpectedFailure.Code => ErrorCode.AnalyticsSettingsModified;
}
