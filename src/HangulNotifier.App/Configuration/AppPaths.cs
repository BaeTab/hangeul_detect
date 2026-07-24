namespace HangulNotifier.App.Configuration;

/// <summary>%APPDATA%\HangulNotifier 하위 표준 경로.</summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HangulNotifier");

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string StatsDb => Path.Combine(Root, "stats.db");
    public static string UserRulesFile => Path.Combine(Root, "user-rules.json");
    public static string LogsDir => Path.Combine(Root, "logs");

    public static void EnsureRoot() => Directory.CreateDirectory(Root);
}
