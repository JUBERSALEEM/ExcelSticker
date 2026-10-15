using System.Windows;

namespace ExcelSticker.Wpf.Studio;

/// <summary>Screen-only scale; never used by LabelView, DocumentPage or printer tickets.</summary>
public static class PreviewZoom
{
    public static double Fit(Size paper,Size viewport,double padding)
    {
        if(!double.IsFinite(padding) || padding<0 || paper.IsEmpty || viewport.IsEmpty || !double.IsFinite(paper.Width) || !double.IsFinite(paper.Height)
            || !double.IsFinite(viewport.Width) || !double.IsFinite(viewport.Height)
            || paper.Width<=0 || paper.Height<=0 || viewport.Width<=padding || viewport.Height<=padding)return 1;
        return Math.Clamp(Math.Min((viewport.Width-padding)/paper.Width,(viewport.Height-padding)/paper.Height),.01,3);
    }
}
