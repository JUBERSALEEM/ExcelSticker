using System.Windows;
using ExcelSticker.Theming;
using System.Windows.Controls;

namespace ExcelSticker.Wpf;

/// <summary>App-owned dialogs share the clay theme; file/printer dialogs remain native Windows UI.</summary>
internal static class StudioDialog
{
    public static bool Confirm(Window owner, string title, string message) => Show(owner, title, message, true, null).Accepted;
    public static void Error(Window owner, string message) => Show(owner, "Please check", message, false, null);
    public static string? ChooseSheet(Window owner, IReadOnlyList<string> sheets)
        => Show(owner, "Choose an Excel worksheet", "The first nonempty row must contain column headings. Select a worksheet to import; click any imported entry to preview its stickers.", true, sheets).Choice;

    private static (bool Accepted, string? Choice) Show(Window owner, string title, string message, bool confirm, IReadOnlyList<string>? choices)
    {
        var dialog = new Window
        {
            Title = "Ocean Glass Label Studio", Owner = owner.IsVisible ? owner : null, Width = 520, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        var content = new StackPanel();
        var card = new ClayCard { Style = (Style)owner.FindResource("Card"), Content = content, Margin = new Thickness(20) };
        dialog.Content = card;
        content.Children.Add(new TextBlock { Text = title, FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14), TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) });
        ComboBox? selector = null;
        if (choices != null)
        {
            selector = new ComboBox { ItemsSource = choices, SelectedIndex = 0 };
            content.Children.Add(selector);
        }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        if (confirm)
        {
            var cancel = new Button { Content = "Cancel", IsCancel = true, IsDefault = choices == null };
            cancel.Click += (_, _) => dialog.DialogResult = false;
            actions.Children.Add(cancel);
        }
        var accept = new Button { Content = choices != null ? "Import worksheet" : confirm ? "Continue" : "OK", IsDefault = !confirm || choices != null, Style = (Style)owner.FindResource("Primary") };
        accept.Click += (_, _) => dialog.DialogResult = true;
        actions.Children.Add(accept);
        content.Children.Add(actions);
        var accepted = dialog.ShowDialog() == true;
        return (accepted, accepted ? selector?.SelectedItem as string : null);
    }
}
