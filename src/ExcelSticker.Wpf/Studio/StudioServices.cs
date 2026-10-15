using System.IO;
using System.Windows;
using ExcelSticker.Theming;

namespace ExcelSticker.Wpf.Studio;

internal static class StudioServices
{
    private static ThemeEngine? _theme;
    public static ThemeEngine Theme => _theme ??= new ThemeEngine(Application.Current.Resources, Path.Combine(AppDiagnostics.DataDirectory, "theme.json"));
    public static string LayoutPath => Path.Combine(AppDiagnostics.DataDirectory,"sticker-layouts.json");
}
