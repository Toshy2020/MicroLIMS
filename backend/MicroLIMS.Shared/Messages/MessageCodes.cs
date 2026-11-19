namespace MicroLIMS.Shared.Messages;

// Codes for the refusals the server reports, named Module.Reason. The frontend
// translates a code through frontend/src/locales/{en,ar}/serverMessages.json;
// the English sentence in the exception stays the fallback and the text every
// log, audit record and test sees.
//
// Every code is listed once in ParameterNames with the placeholders its
// sentence uses ({{name}} in the template files). MessageTemplateTests keeps
// the two template files and this list in step.
//
// From F4 Task 4 on, a new BusinessRuleException / NotFoundException in
// Application or API carries one of these codes (UncodedMessageRatchetTests).
public static partial class MessageCodes
{
    // Code -> the parameter names its sentence uses. Each prefix lives in its own file
    // (MessageCodes.<Prefix>.cs); a code listed twice fails here with a duplicate-key error.
    public static IReadOnlyDictionary<string, string[]> ParameterNames { get; } = Combine(
        AfterCleaningParameters(),
        AssayParameters(),
        AuditReviewParameters(),
        AuthParameters(),
        ControlChartParameters(),
        ColumnParameters(),
        CryovialParameters(),
        DisintegrationParameters(),
        DissolutionParameters(),
        DocumentParameters(),
        EquipmentConfigParameters(),
        EquipmentPlanParameters(),
        ExportParameters(),
        HplcParameters(),
        IcpParameters(),
        IncubationParameters(),
        MaterialParameters(),
        MediaParameters(),
        MonitoringParameters(),
        NotificationParameters(),
        OosParameters(),
        OrganismParameters(),
        OutsourcedParameters(),
        PathogenParameters(),
        ReceivingParameters(),
        ReferenceListParameters(),
        ResultParameters(),
        SampleParameters(),
        SettingsParameters(),
        SolutionParameters(),
        SpecificationParameters(),
        StandardParameters(),
        TableViewParameters(),
        TestDefinitionParameters(),
        TestParameters(),
        TitrantParameters(),
        TitrationParameters(),
        UserParameters(),
        UvParameters(),
        WasteParameters(),
        WeightVariationParameters(),
        WorkflowStepParameters());

    private static Dictionary<string, string[]> Combine(params Dictionary<string, string[]>[] parts)
    {
        var all = new Dictionary<string, string[]>();
        foreach (var part in parts)
            foreach (var (code, names) in part)
                all.Add(code, names);
        return all;
    }
}
