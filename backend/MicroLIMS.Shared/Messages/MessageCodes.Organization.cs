namespace MicroLIMS.Shared.Messages;

// Organization.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string OrganizationAreaOnlyForPhyschemMembership = "Organization.AreaOnlyForPhyschemMembership";
    public const string OrganizationClosureReasonRequired = "Organization.ClosureReasonRequired";
    public const string OrganizationClosureRequiresOtherLabRejection = "Organization.ClosureRequiresOtherLabRejection";
    public const string OrganizationDepartmentsNotFoundOrInactive = "Organization.DepartmentsNotFoundOrInactive";
    public const string OrganizationInactiveUserCannotAssignToSections = "Organization.InactiveUserCannotAssignToSections";
    public const string OrganizationNoOpenTestsOnSample = "Organization.NoOpenTestsOnSample";
    public const string OrganizationSampleHasNoTestsInSection = "Organization.SampleHasNoTestsInSection";
    public const string OrganizationSectionNotFoundOrInactive = "Organization.SectionNotFoundOrInactive";
    public const string OrganizationSectionNotInDepartment = "Organization.SectionNotInDepartment";
    public const string OrganizationTestingAlreadyClosed = "Organization.TestingAlreadyClosed";
    public const string OrganizationUnknownLaboratory = "Organization.UnknownLaboratory";

    private static Dictionary<string, string[]> OrganizationParameters() => new()
    {
        [OrganizationAreaOnlyForPhyschemMembership] = [],
        [OrganizationClosureReasonRequired] = [],
        [OrganizationClosureRequiresOtherLabRejection] = [],
        [OrganizationDepartmentsNotFoundOrInactive] = [],
        [OrganizationInactiveUserCannotAssignToSections] = [],
        [OrganizationNoOpenTestsOnSample] = [],
        [OrganizationSampleHasNoTestsInSection] = [],
        [OrganizationSectionNotFoundOrInactive] = ["sectionId"],
        [OrganizationSectionNotInDepartment] = ["departmentId", "sectionId"],
        [OrganizationTestingAlreadyClosed] = [],
        [OrganizationUnknownLaboratory] = ["labCode"],
    };
}
