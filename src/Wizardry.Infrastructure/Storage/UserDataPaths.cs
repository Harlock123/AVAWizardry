namespace Wizardry.Infrastructure.Storage;

/// <summary>
/// Per-user locations for saves and settings, using .NET's (8+) platform mappings:
/// saves  -> LocalApplicationData: %LOCALAPPDATA% | $XDG_DATA_HOME or ~/.local/share | ~/Library/Application Support
/// config -> ApplicationData:      %APPDATA%      | $XDG_CONFIG_HOME or ~/.config    | ~/Library/Application Support
/// </summary>
public sealed class UserDataPaths(string dataRoot, string configRoot)
{
    public const string AppFolderName = "AVAWizardry";

    public string DataRoot { get; } = dataRoot;
    public string ConfigRoot { get; } = configRoot;
    public string SaveDirectory => Path.Combine(DataRoot, "saves");
    public string SettingsFile => Path.Combine(ConfigRoot, "settings.json");

    public static UserDataPaths ForCurrentUser() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), AppFolderName),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), AppFolderName));
}
