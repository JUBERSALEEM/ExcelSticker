using System.Windows;
using System.Windows.Documents;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

/// <summary>Creates pages on demand, rather than allocating thousands of WPF controls up front.</summary>
public sealed class LabelPaginator : DocumentPaginator
{
    private readonly GlassLabel[] _pages;
    private readonly CompanyProfile _company;
    private static readonly Size Stock = new(LabelView.LabelWidth, LabelView.LabelHeight);

    public LabelPaginator(IEnumerable<GlassLabel> labels, bool repeatQuantity, CompanyProfile company)
    {
        _pages = labels.SelectMany(label => Enumerable.Repeat(label, repeatQuantity ? label.Qty : 1)).ToArray();
        _company = company;
    }
    public override bool IsPageCountValid => true;
    public override int PageCount => _pages.Length;
    public override Size PageSize { get => Stock; set { } }
    public override IDocumentPaginatorSource Source => null!;
    public override DocumentPage GetPage(int pageNumber)
    {
        if (pageNumber < 0 || pageNumber >= PageCount) return DocumentPage.Missing;
        var visual = new LabelView(_pages[pageNumber], _company);
        visual.Measure(Stock);
        visual.Arrange(new Rect(Stock));
        visual.UpdateLayout();
        return new DocumentPage(visual, Stock, new Rect(Stock), new Rect(Stock));
    }
}
