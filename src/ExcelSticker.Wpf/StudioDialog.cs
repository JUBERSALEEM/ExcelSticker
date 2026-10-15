using System.Windows;
using ExcelSticker.Theming;
using System.Windows.Controls;

namespace ExcelSticker.Wpf;

/// <summary>App-owned dialogs share the clay theme; file/printer dialogs remain native Windows UI.</summary>
internal static class StudioDialog
{
    public static void Info(Window owner, string title, string message) => Show(owner, title, message, false, null, true);
    public static bool Confirm(Window owner, string title, string message) => Show(owner, title, message, true, null).Accepted;
    public static void Error(Window owner, string message) => Show(owner, "Please check", message, false, null);
    public static string? ChooseSheet(Window owner, IReadOnlyList<string> sheets)
        => Show(owner, "Choose an Excel worksheet", "Select a worksheet. The next step lets you choose the header row, map columns and validate before importing.", true, sheets).Choice;

    private static (bool Accepted, string? Choice) Show(Window owner, string title, string message, bool confirm, IReadOnlyList<string>? choices, bool selectable = false)
    {
        var dialog = new Window
        {
            Title = "LABLE STUDIO", Owner = owner.IsVisible ? owner : null, Width = 520, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        var content = new StackPanel();
        var card = new ClayCard { Style = (Style)owner.FindResource("Card"), Content = content };
        card.SetResourceReference(FrameworkElement.MarginProperty,"ClayWorkspaceMargin");
        dialog.Content = card;
        content.Children.Add(new TextBlock { Text = title, FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14), TextWrapping = TextWrapping.Wrap });
        if (selectable) content.Children.Add(new TextBox { Text = message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 220, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 20) });
        else content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) });
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
