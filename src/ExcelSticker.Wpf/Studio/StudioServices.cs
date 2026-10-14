using System.IO;
using System.Windows;
using ExcelSticker.Theming;

namespace ExcelSticker.Wpf.Studio;

internal static class StudioServices
{
    private static ThemeEngine? _theme;
    public static ThemeEngine Theme => _theme ??= new ThemeEngine(Application.Current.Resources);
    public static string LayoutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OceanGlass","sticker-layouts.json");
}
