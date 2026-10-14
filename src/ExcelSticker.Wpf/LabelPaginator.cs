using System.Windows;
using System.Windows.Documents;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

public sealed class LabelPaginator : DocumentPaginator
{
    private readonly GlassLabel[] _pages;
    private readonly CompanyProfile _company;
    private readonly LayoutLibrary _layouts;
    private readonly StickerFormat? _format;
    private static readonly Size Stock=new(LabelView.LabelWidth,LabelView.LabelHeight);
    public LabelPaginator(IEnumerable<GlassLabel> labels,bool repeatQuantity,CompanyProfile company,LayoutLibrary? layouts=null,StickerFormat? format=null,NumberingStyle numbering=NumberingStyle.EntryCopy)
    {
        _pages=LabelPrintPlan.Build(labels,repeatQuantity,numbering);_company=company;_layouts=(layouts??new LayoutLibrary()).Clone();_layouts.Validate();_format=format;
    }
    public override bool IsPageCountValid=>true;
    public override int PageCount=>_pages.Length;
    public override Size PageSize{get=>Stock;set{}}
    public override IDocumentPaginatorSource Source=>null!;
    public override DocumentPage GetPage(int pageNumber)
    {
        if(pageNumber<0||pageNumber>=PageCount)return DocumentPage.Missing;
        var label=_pages[pageNumber];
        var view=new LabelView(label,_company,_layouts.For(_format??LayoutLibrary.FormatFor(label)));
        view.Measure(Stock);view.Arrange(new Rect(Stock));view.UpdateLayout();
        return new DocumentPage(view,Stock,new Rect(Stock),new Rect(Stock));
    }
}
