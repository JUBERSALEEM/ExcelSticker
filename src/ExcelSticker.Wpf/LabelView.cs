using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

/// <summary>One physical label at WPF's 96 device-independent units per inch.</summary>
public sealed class LabelView : Border
{
    public const double LabelWidth = 100 * 96.0 / 25.4;
    public const double LabelHeight = 70 * 96.0 / 25.4;

    public GlassLabel Label { get; }

    public LabelView(GlassLabel label, CompanyProfile company)
    {
        Label = label;
        Width = LabelWidth;
        Height = LabelHeight;
        Padding = new Thickness(16, 12, 16, 12);
        Background = Brushes.White;
        SnapsToDevicePixels = true;
        var grid = new Grid();
        Child = grid;
        foreach (var height in new[] { 29.0, 1, 44, 26, 28, 28, 26, 18 })
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Add(FittedText(company.Name, 21), 0);
        Add(new Border { Background = Brushes.Black }, 1);
        var contact = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 0, 1), Width = 210 };
        foreach (var line in new[] { company.Address, company.Phone, company.Website, company.Email })
            contact.Children.Add(FittedText(line, 8));
        Add(contact, 2);
        var order = new Grid();
        order.ColumnDefinitions.Add(new ColumnDefinition());
        order.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
        var orderText = FittedText($"Order: {label.OrderNo}", 16);
        orderText.Margin = new Thickness(0, 0, 8, 0);
        order.Children.Add(orderText);
        var quantity = FittedText($"QTY: {label.Qty}", 16);
        quantity.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(quantity, 1);
        order.Children.Add(quantity);
        Add(order, 3);
        Add(FittedText(label.GlassType, 17), 4);
        Add(FittedText($"SIZE: {label.Width.ToString("0.0", CultureInfo.InvariantCulture)} X {label.Height.ToString("0.0", CultureInfo.InvariantCulture)}", 17), 5);
        Add(FittedText($"REF: {label.ReferenceNo}", 16), 6);
        var detail = string.Join("  ·  ", new[] { label.Pane, string.IsNullOrEmpty(label.JobNo) ? "" : $"JOB: {label.JobNo}" }.Where(value => value.Length > 0));
        Add(FittedText(detail, 12), 7);
        var customer = FittedText(label.CustomerName, 17, underline: true);
        customer.HorizontalAlignment = HorizontalAlignment.Center;
        customer.VerticalAlignment = VerticalAlignment.Bottom;
        Add(customer, 8);

        void Add(UIElement element, int row) { Grid.SetRow(element, row); grid.Children.Add(element); }
    }

    private static Viewbox FittedText(string text, double size, bool underline = false) => new()
    {
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text, FontFamily = new FontFamily("Arial"), FontSize = size,
            FontWeight = size > 10 ? FontWeights.Bold : FontWeights.Normal,
            Foreground = Brushes.Black,
            TextDecorations = underline ? TextDecorations.Underline : null
        }
    };
}
