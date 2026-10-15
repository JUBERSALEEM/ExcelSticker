using System.IO;
using System.Reflection;
using System.Text;
namespace ExcelSticker.Wpf;

internal static class AppBuildInfo
{
    private static readonly Assembly BuildAssembly = typeof(AppBuildInfo).Assembly;
    private static string Meta(string name) => BuildAssembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == name)?.Value ?? "unavailable";
    public static string Version => BuildAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unavailable";
    public static string Short => $"v{Version} · {Meta("BuildConfiguration")} · {Meta("BuildCommit")}";
    public static string Describe() => string.Join(Environment.NewLine, new[] {
        "LABLE STUDIO — " + Short, "Source branch: " + Meta("BuildBranch"), "Source commit: " + Meta("BuildCommit"),
        "Built UTC: " + Meta("BuildUtc"), "Application folder: " + AppContext.BaseDirectory,
        "Loaded assembly: " + BuildAssembly.Location, "Settings: " + AppDiagnostics.DataDirectory,
        "Log: " + AppDiagnostics.LogPath,
        "Commit identifies the checkout at build time; local uncommitted edits are not encoded in the hash. Git information is unavailable for ZIPs or without git on PATH." });
}
internal static class AppDiagnostics
{
    private static readonly object Gate = new();
    public static string DataDirectory => Environment.GetEnvironmentVariable("OCEANGLASS_DATA_DIR") is { Length: > 0 } custom
        ? Path.GetFullPath(custom) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OceanGlass");
    public static string LogPath => Path.Combine(DataDirectory, "logs", "app.log");
    public static void Log(string action, Exception? exception = null)
    {
        try {
            lock (Gate) {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 2 * 1024 * 1024) File.Move(LogPath, LogPath + ".previous", true);
                File.AppendAllText(LogPath, $"{DateTime.UtcNow:O} [{AppBuildInfo.Short}] {action}\n{exception}\n", new UTF8Encoding(false));
            }
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
