namespace MicroLIMS.Shared.Messages;

// TableView.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string TableViewMaxPersonalViewsReached = "TableView.MaxPersonalViewsReached";
    public const string TableViewNameAlreadyExistsHere = "TableView.NameAlreadyExistsHere";
    public const string TableViewNameAlreadySharedWithSection = "TableView.NameAlreadySharedWithSection";
    public const string TableViewNameClashOnSave = "TableView.NameClashOnSave";
    public const string TableViewNameRequired = "TableView.NameRequired";
    public const string TableViewNameTooLong = "TableView.NameTooLong";
    public const string TableViewNotFound = "TableView.NotFound";
    public const string TableViewPersonalNameExists = "TableView.PersonalNameExists";
    public const string TableViewPersonalViewNoSection = "TableView.PersonalViewNoSection";
    public const string TableViewSectionRequired = "TableView.SectionRequired";
    public const string TableViewSettingsInvalidJson = "TableView.SettingsInvalidJson";
    public const string TableViewSettingsMissing = "TableView.SettingsMissing";
    public const string TableViewSettingsMissingVersion = "TableView.SettingsMissingVersion";
    public const string TableViewSettingsTooLarge = "TableView.SettingsTooLarge";
    public const string TableViewTableNotSupported = "TableView.TableNotSupported";
    public const string TableViewUpdateEmpty = "TableView.UpdateEmpty";
    public const string TableViewWrongTable = "TableView.WrongTable";

    private static Dictionary<string, string[]> TableViewParameters() => new()
    {
        [TableViewMaxPersonalViewsReached] = ["max"],
        [TableViewNameAlreadyExistsHere] = ["name"],
        [TableViewNameAlreadySharedWithSection] = ["name"],
        [TableViewNameClashOnSave] = ["name"],
        [TableViewNameRequired] = [],
        [TableViewNameTooLong] = ["max"],
        [TableViewNotFound] = ["viewId"],
        [TableViewPersonalNameExists] = ["name"],
        [TableViewPersonalViewNoSection] = [],
        [TableViewSectionRequired] = [],
        [TableViewSettingsInvalidJson] = [],
        [TableViewSettingsMissing] = [],
        [TableViewSettingsMissingVersion] = [],
        [TableViewSettingsTooLarge] = ["maxKb"],
        [TableViewTableNotSupported] = [],
        [TableViewUpdateEmpty] = [],
        [TableViewWrongTable] = [],
    };
}
