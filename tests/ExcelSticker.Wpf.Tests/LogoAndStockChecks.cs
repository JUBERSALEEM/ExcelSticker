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
            Check(preview.CanPrint && ((TextBlock)preview.FindName("PrinterSummary")).Text.Contains("first print"),"Changing stock clears the old printer summary without introducing another approval gate");
            Check(Math.Abs(PreviewZoom.Fit(new Size(1200,1200),new Size(600,400),40)-.3)<.000001,"Fit-sticker zoom chooses the limiting viewport axis without changing physical page dimensions");
            Check(PreviewZoom.Fit(new Size(400,200),new Size(440,240),40)==1 && PreviewZoom.Fit(new Size(20,20),new Size(500,500),40)==3,"Screen fit preserves aspect ratio and caps excessive enlargement");
            Check(PreviewZoom.Fit(new Size(400,200),new Size(0,0),40)==1,"Unmeasured preview viewports have a finite scale");
            preview.Measure(new Size(1000,1500));preview.Arrange(new Rect(0,0,1000,1500));preview.UpdateLayout();
            var viewport=(ScrollViewer)preview.FindName("PageViewport");var zoom=(ScaleTransform)((Border)preview.FindName("PaperZoom")).LayoutTransform;
            Check(preview.Job!.PageSize.Width*zoom.ScaleX<=viewport.ActualWidth-32 && preview.Job.PageSize.Height*zoom.ScaleY<=viewport.ActualHeight-32,"Fit sticker keeps the entire prepared custom-stock paper inside the preview viewport");
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
            Dictionary<string,TextBox> Bounds()=>Private<Dictionary<string,TextBox>>("_geometryInputs");
            StickerElement Current()=>Private<LayoutLibrary>("_draft").For(StickerFormat.SGU).Elements.Last();
            void Reject(string method)
            {
                try{Invoke(method);}catch(System.Reflection.TargetInvocationException ex) when(ex.InnerException is FormatException){return;}
                throw new Exception("Expected geometry/stock validation to reject "+method);
            }
            Bounds()["X"].Text="110";Bounds()["Width"].Text="15";Invoke("ApplyGeometry");
            Bounds()["Width"].Text="60";Bounds()["X"].Text="10";
            Check(Current().X==110 && Current().Width==15,"Geometry inputs stage interdependent width and position edits without partial model mutation");
            Invoke("ApplyGeometry");Check(Current().X==10 && Current().Width==60,"Position and width apply as one valid rectangle even when the intermediate width would overflow");
            Invoke("Undo");Check(Current().X==110 && Current().Width==15,"A combined position/size edit undoes in one step");
            Bounds()["X"].Text="129";Reject("ApplyGeometry");
            Check(Current().X==110 && Bounds()["X"].Text=="129","Invalid geometry neither mutates the model nor silently discards the typed value");
            var selectedLogo=Current();var fields=Private<ListBox>("_elements");fields.SelectedItem=Private<LayoutLibrary>("_draft").For(StickerFormat.SGU).Elements.First();
            Check(ReferenceEquals(fields.SelectedItem,selectedLogo) && Bounds()["X"].Text=="129","Changing selection cannot discard an invalid pending rectangle");
            Private<StackPanel>("_properties").Children.OfType<WrapPanel>().SelectMany(p=>p.Children.OfType<Button>()).Single(b=>Equals(b.Content,"Reset values")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Bounds()["X"].Text=="110","Reset values explicitly discards invalid geometry input");
            Current().Height=1.234567;Invoke("Refresh");Bounds()["X"].Text="12";Invoke("CommitForSave");
            Check(Current().X==12 && Current().Height==1.234567,"Save commits pending bounds without rounding untouched imported coordinates");
            Private<TextBox>("_stockWidth").Text="160";Reject("CommitForSave");
            Check(Private<LayoutLibrary>("_draft").For(StickerFormat.SGU).WidthMm==130,"Save cannot silently ignore unapplied stock dimensions");
            Private<TextBox>("_stockWidth").Text="130";
            typeof(StickerEditorWindow).GetField("_saved",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(editor,true);editor.Close();
        }
        finally{Directory.Delete(folder,true);}
    }
}
