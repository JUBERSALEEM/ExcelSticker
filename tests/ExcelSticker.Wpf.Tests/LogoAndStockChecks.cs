using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExcelSticker.Core;
using ExcelSticker.Wpf;
using ExcelSticker.Wpf.Studio;

internal static class LogoAndStockChecks
{
    public static void Run()
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
        var folder=Path.Combine(Path.GetTempPath(),"sticker-logo-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            var input=Path.Combine(folder,"logo.png");var pixels=new byte[64*32*4];
            for(var y=0;y<32;y++)for(var x=0;x<32;x++)pixels[(y*64+x)*4+3]=255;
            var bitmap=BitmapSource.Create(64,32,96,96,PixelFormats.Bgra32,null,pixels,64*4);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(input))encoder.Save(file);
            var data=LogoImages.Import(input);File.Delete(input);var decoded=LogoImages.Decode(data)!;
            var gray=new byte[64*32];decoded.CopyPixels(gray,64,0);
            Check(decoded.IsFrozen && decoded.Format==PixelFormats.Gray8 && gray[0]==0 && gray[63]==255,"Logo import flattens transparency onto white and produces frozen grayscale after the original is deleted");
            var rotated=LogoImages.Rotated(data,90)!;
            Check(rotated.PixelWidth==32 && rotated.PixelHeight==64,"Logo rotation swaps bitmap axes without clipping the selected box");
            var library=new LayoutLibrary();library.ResizeStock(120,80,true);
            var layout=library.For(StickerFormat.SGU);
            layout.Elements.Add(new StickerElement {Field="Logo",X=5,Y=5,Width=15,Height=8,ImagePng=data});
            var path=Path.Combine(folder,"layouts.json");library.Save(path);library=LayoutLibrary.Load(path);LogoImages.ValidateLibrary(library);
            Check(library.For(StickerFormat.SGU).Elements.Last().ImagePng==data,"Saved and reopened layouts retain the embedded logo, not its original file path");
            var job=new LabelPaginator(new[]{LiveStickerSamples.Sgu().Outer with {EntryNumber=7}},true,new CompanyProfile(),library);
            library.ResizeStock(140,90,true);library.For(StickerFormat.SGU).Elements.Last().ImagePng="";
            var page=job.GetPage(3);var view=(LabelView)page.Visual;var image=((Canvas)view.Child).Children.OfType<Image>().Single();
            Check(Math.Abs(page.Size.Width-120*LabelView.UnitsPerMm)<.001 && Math.Abs(page.Size.Height-80*LabelView.UnitsPerMm)<.001 && view.Width==page.Size.Width,"Physical page and renderer use the saved custom stock, not the old fixed constants");
            Check(image.Source!=null && image.Stretch==Stretch.Uniform && view.Label.QuantityText=="7/4","Snapshot retains the original logo and exact copy number after live layout mutations");
            var selected=job.SelectPages(3,1);Check(selected.PageSize==job.PageSize && ((LabelView)selected.GetPage(0).Visual).Width==view.Width,"Current/range printing retains custom paper geometry");
            var preview=new PrintJobPreview();preview.LoadJob(job,"logo stock test");
            var paper=(Grid)preview.FindName("PaperSize");Check(paper.Width==view.Width && paper.Height==view.Height,"Inline job paper frame follows actual custom page size");
            preview.SetPrinter(new PrinterReview("test",true,null,null,Array.Empty<string>()));
            preview.LoadJob(new LabelPaginator(new[]{LiveStickerSamples.Sgu().Outer},false,new CompanyProfile(),library),"new stock");
            Check(!preview.CanPrint,"Changing prepared stock clears old printer approval");
            var ticket=new PrintTicket {PageMediaSize=new PageMediaSize(job.PageSize.Width,job.PageSize.Height),PageOrientation=PageOrientation.Portrait,PageScalingFactor=100,CopyCount=1,PagesPerSheet=1};
            Check(PrinterReview.Inspect("test",ticket,null,job.PageSize).StockVerified && !PrinterReview.Inspect("test",ticket,null).StockVerified,"Printer stock validation uses requested custom media rather than accepting only 100 × 70");

            // Exercise editor controls without file dialogs or real printing.
            var editor=new StickerEditorWindow(library,new CompanyProfile(),path,StickerFormat.SGU);
            T Private<T>(string field)=>(T)typeof(StickerEditorWindow).GetField(field,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(editor)!;
            void Invoke(string method)=>typeof(StickerEditorWindow).GetMethod(method,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(editor,null);
            typeof(StickerEditorWindow).GetMethod("ApplyLogo",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(editor,new object[]{data});
            Check(Private<LayoutLibrary>("_draft").Layouts.All(l=>l.Elements.Any(e=>e.Field=="Logo" && e.ImagePng==data)),"Import-all applies an embedded logo to SGU, DGU and Job Order transactionally");
            Check(Private<LayoutLibrary>("_draft").For(StickerFormat.DGU).Elements.Single(e=>e.Field=="CompanyName").X>20,"First logo import reserves space in the default company header");
            Private<TextBox>("_stockWidth").Text="130";Private<TextBox>("_stockHeight").Text="85";Invoke("ApplyStock");
            Check(Private<LayoutLibrary>("_draft").Layouts.All(l=>l.WidthMm==130 && l.HeightMm==85),"Editor stock controls apply dimensions to all formats");
            Invoke("Undo");Check(Private<LayoutLibrary>("_draft").Layouts.All(l=>l.WidthMm==140),"Stock adjustment participates in layout undo");
            Invoke("Redo");Check(Private<LayoutLibrary>("_draft").Layouts.All(l=>l.WidthMm==130),"Stock adjustment participates in layout redo");
            // Do not show/close a dirty modal window in this non-interactive test.
            var draft=Private<LayoutLibrary>("_draft");var logo=draft.For(StickerFormat.SGU).Elements.Last();
            Private<ListBox>("_elements").SelectedItem=logo;
            var inspector=Private<StackPanel>("_properties");
            var rotate=inspector.Children.OfType<Button>().Single(b=>Equals(b.Content,"Rotate logo 90°"));rotate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Private<LayoutLibrary>("_draft").For(StickerFormat.SGU).Elements.Last().Rotation==90,"Logo inspector rotation command updates the saved model");
            inspector=Private<StackPanel>("_properties");var move=inspector.Children.OfType<WrapPanel>().SelectMany(p=>p.Children.OfType<Button>()).Single(b=>Equals(b.Content,"→"));
            var before=Private<LayoutLibrary>("_draft").For(StickerFormat.SGU).Elements.Last().X;move.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Private<LayoutLibrary>("_draft").For(StickerFormat.SGU).Elements.Last().X==before+.5,"Visible move controls nudge the logo precisely without needing mouse dragging");
            typeof(StickerEditorWindow).GetField("_saved",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(editor,true);editor.Close();
        }
        finally{Directory.Delete(folder,true);}
    }
}
