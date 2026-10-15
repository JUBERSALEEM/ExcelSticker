using System.Windows;
using System.Windows.Documents;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

/// <summary>Exactly the displayed label, including its existing copy number. No quantity expansion or preview-page dependency.</summary>
public sealed class LiveStickerPaginator : DocumentPaginator
{
    private readonly GlassLabel _label;
    private readonly CompanyProfile _company;
    private readonly StickerLayout _layout;
    public LiveStickerPaginator(LabelView live,CompanyProfile company)
    {_label=live.Label;_company=company.Clone();_layout=live.Layout.Clone();_layout.Validate();}
    public override bool IsPageCountValid=>true;
    public override int PageCount=>1;
    public override Size PageSize {get=>new(_layout.WidthMm*LabelView.UnitsPerMm,_layout.HeightMm*LabelView.UnitsPerMm);set{}}
    public override IDocumentPaginatorSource Source=>null!;
    public override DocumentPage GetPage(int index)
    {
        if(index!=0)return DocumentPage.Missing;
        var view=new LabelView(_label,_company,_layout);view.Measure(PageSize);view.Arrange(new Rect(PageSize));view.UpdateLayout();
        return new DocumentPage(view,PageSize,new Rect(PageSize),new Rect(PageSize));
    }
}
