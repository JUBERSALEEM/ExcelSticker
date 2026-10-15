using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExcelSticker.Core;
using ExcelSticker.Wpf;
using ExcelSticker.Wpf.Studio;

internal static class CompanyAndToneChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    public static void Run()
    {
        var folder=Path.Combine(Path.GetTempPath(),"studio-company-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var editor=(StickerEditorWindow?)null;
        try
        {
            var company=new CompanyProfile {Name="ORIGINAL FACTORY"};var library=new LayoutLibrary();
            library.Layouts=library.Layouts.Select(l=>l.CompanyPreset()).ToList();
            var path=Path.Combine(folder,"layouts.json");
            editor=new StickerEditorWindow(library,company,path,StickerFormat.SGU);
            T Get<T>(string name)=>(T)typeof(StickerEditorWindow).GetField(name,Private)!.GetValue(editor)!;
            void Invoke(string method)=>typeof(StickerEditorWindow).GetMethod(method,Private)!.Invoke(editor,null);
            void Reject(string method)
            {
                try{Invoke(method);}catch(TargetInvocationException ex) when(ex.InnerException is FormatException){return;}
                throw new Exception("Expected validation to block "+method);
            }
            string[] Texts(LabelView view)=>((Canvas)view.Child).Children.OfType<TextBlock>().Select(t=>t.Text).ToArray();
            LabelView Preview()=>(LabelView)Get<ContentControl>("_render").Content;
            var fields=Get<Dictionary<string,TextBox>>("_companyInputs");
            fields["Name"].Text="MY NEW FACTORY";fields["Address"].Text="Abu Dhabi";fields["Phone"].Text="+971 123";fields["Website"].Text="factory.example";fields["Email"].Text="team@factory.example";
            Check(Texts(Preview()).Contains("MY NEW FACTORY") && Texts(Preview()).Contains("team@factory.example"),"Typing company details updates Studio preview before Apply");
            Check(company.Name=="ORIGINAL FACTORY" && library.Company==null && !File.Exists(path),"Unsaved company edits cannot mutate caller profile, library or disk");
            Invoke("ApplyCompany");Invoke("Undo");Check(fields["Name"].Text=="ORIGINAL FACTORY","Company details undo as one applied change");
            Invoke("Redo");Check(fields["Name"].Text=="MY NEW FACTORY","Company detail redo restores all shared values");
            Get<ComboBox>("_format").SelectedItem=StickerFormat.DGU;
            Check(Texts(Preview()).Contains("MY NEW FACTORY") && Texts(Preview()).Contains("team@factory.example"),"Company values follow format switching");
            var inspector=Get<Dictionary<string,TextBox>>("_geometryInputs");
            Check(inspector.ContainsKey("Prefix") && !inspector.ContainsKey("Text"),"Bound company fields no longer offer an ineffective Custom text input");
            inspector["FontSize"].Text="not a number";Reject("SaveDraft");
            Check(!File.Exists(path) && inspector["FontSize"].Text=="not a number","Invalid pending font cannot silently save the old value");
            inspector["FontSize"].Text="14";inspector["Prefix"].Text="By: ";Get<TextBox>("_stockWidth").Text="120";Get<TextBox>("_stockHeight").Text="80";
            fields["Phone"].Text="+971 456";Invoke("SaveDraft");
            var saved=LayoutLibrary.Load(path);
            Check(saved.Company!.Name=="MY NEW FACTORY" && saved.Company.Phone=="+971 456" && saved.Layouts.All(l=>l.WidthMm==120 && l.HeightMm==80),"Save commits focused company, font and stock inputs in one persisted library");
            Check(saved.For(StickerFormat.DGU).Elements.Single(e=>e.Field=="CompanyName") is {FontSize:14,Prefix:"By: "},"Save includes staged field font and prefix without a focus-loss dependency");
            var bytes=File.ReadAllText(path);fields["Name"].Text=" ";Reject("SaveDraft");Check(File.ReadAllText(path)==bytes,"Invalid company name cannot overwrite the saved file");fields["Name"].Text="MY NEW FACTORY";
            var source=new PngBitmapEncoder();var gray=Enumerable.Repeat((byte)220,64*32).ToArray();
            for(var y=0;y<32;y++)for(var x=32;x<64;x++)gray[y*64+x]=255;
            source.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64,32,96,96,PixelFormats.Gray8,null,gray,64)));
            using var output=new MemoryStream();source.Save(output);var data=Convert.ToBase64String(output.ToArray());
            byte[] Pixels(BitmapSource bitmap){var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight];bitmap.CopyPixels(pixels,bitmap.PixelWidth,0);return pixels;}
            var original=Pixels(LogoImages.Decode(data)!);var darker=LogoImages.Adjusted(data,3,1)!;
            Check(darker.IsFrozen && Pixels(darker)[0]<original[0] && Pixels(darker)[63]==255,"Logo darkness strengthens gray but preserves white backing");
            var binary=LogoImages.Adjusted(data,2,1,true,220)!;
            Check(Pixels(binary)[0]==0 && Pixels(binary)[63]==255 && Pixels(LogoImages.Adjusted(data)!).SequenceEqual(original),"Solid black mode and Reset are reversible without rewriting source pixels");
            typeof(StickerEditorWindow).GetMethod("ApplyLogo",Private)!.Invoke(editor,new object[]{data});
            var controls=Get<StackPanel>("_properties");
            var slider=controls.Children.OfType<Slider>().Single(s=>System.Windows.Automation.AutomationProperties.GetName(s)=="Logo Darkness");slider.Value=3;
            controls.Children.OfType<Button>().Single(b=>Equals(b.Content,"Copy tones to all formats")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Get<LayoutLibrary>("_draft").Layouts.All(l=>l.Elements.Single(e=>e.Field=="Logo").LogoDarkness==3),"Logo tone controls can update matching artwork across formats");
            Invoke("SaveDraft");saved=LayoutLibrary.Load(path);
            Check(saved.Layouts.All(l=>l.Elements.Single(e=>e.Field=="Logo").ImagePng==data && l.Elements.Single(e=>e.Field=="Logo").LogoDarkness==3),"Logo source and adjustment parameters survive save/reopen");
            var profile=CompanyProfile.FromDetails(saved.Company!);var job=new LabelPaginator(new[]{LiveStickerSamples.Dgu().Outer},true,profile,saved,StickerFormat.DGU);
            saved.Company!.Name="LATER";saved.For(StickerFormat.DGU).Elements.Single(e=>e.Field=="Logo").LogoDarkness=1;
            using var page=job.GetPage(0);var printed=(LabelView)page.Visual;
            Check(Texts(printed).Contains("By: MY NEW FACTORY") && printed.Layout.Elements.Single(e=>e.Field=="Logo").LogoDarkness==3,"Prepared print snapshots freeze company text and logo tones");
            var image=((Canvas)printed.Child).Children.OfType<Image>().Single();
            Check(Pixels((BitmapSource)image.Source)[0]==Pixels(darker)[0],"Shared Windows/preview renderer uses adjusted logo pixels");
            var isolated=new StickerLayout {Elements=new(){new StickerElement {Field="Logo",X=4,Y=4,Width=16,Height=8,ImagePng=data,LogoBlackWhite=true,LogoThreshold=240}}};
            var raster=ZebraRasterizer.Render(new LabelView(LiveStickerSamples.Sgu().Outer,profile,isolated),new ZebraZt411(8));
            bool Black(int x,int y)=>(raster.BlackBits[y*raster.Stride+x/8]&(128>>(x%8)))!=0;
            Check(Black(6*8,6*8) && !Black(18*8,6*8),"Final ZPL raster receives enhanced black artwork and white background");
            // Verify real startup precedence without showing the window or probing printers.
            var settings=Path.Combine(AppDiagnostics.DataDirectory,"sticker-layouts.json");var old=File.Exists(settings)?File.ReadAllBytes(settings):null;
            try
            {
                LayoutLibrary.Load(path).Save(settings);var reopened=new MainWindow();
                try
                {
                    Check(Texts((LabelView)((ContentControl)reopened.FindName("PreviewHost")).Content).Any(t=>t.Contains("MY NEW FACTORY")),"Application restart loads saved company details instead of bundled defaults");
                    var previous=reopened.PreparePrintPreview();var updated=LayoutLibrary.Load(path);updated.Company!.Name="UPDATED THIS SESSION";
                    typeof(MainWindow).GetMethod("ApplyStickerLibrary",Private)!.Invoke(reopened,new object[]{updated});
                    Check(Texts((LabelView)((ContentControl)reopened.FindName("PreviewHost")).Content).Any(t=>t.Contains("UPDATED THIS SESSION")),"Applying saved Studio settings updates the live sticker without restarting");
                    using var livePage=reopened.CaptureLiveSticker().GetPage(0);
                    Check(Texts((LabelView)livePage.Visual).Any(t=>t.Contains("UPDATED THIS SESSION")),"Direct live printing uses the updated company immediately");
                    var preview=(PrintJobPreview)reopened.FindName("JobPreview");using var batchPage=preview.Job!.GetPage(0);
                    Check(!preview.IsStale && Texts((LabelView)batchPage.Visual).Any(t=>t.Contains("UPDATED THIS SESSION")),"An open sample batch refreshes to updated company details");
                    using var oldPage=previous.GetPage(0);Check(Texts((LabelView)oldPage.Visual).Any(t=>t.Contains("MY NEW FACTORY")),"Previously captured print snapshots remain immutable after company update");
                }
                finally{reopened.Close();}
            }
            finally{if(old==null)File.Delete(settings);else File.WriteAllBytes(settings,old);}
        }
        finally
        {
            if(editor!=null){typeof(StickerEditorWindow).GetField("_saved",Private)!.SetValue(editor,true);editor.Close();}
            Directory.Delete(folder,true);
        }
    }
}
