using System.IO;
using System.Text;
using System.Windows.Documents;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

internal static class ZebraPrintJob
{
    public static async Task SendAsync(DocumentPaginator pages,ZebraZt411 zebra,CancellationToken cancellation,Action<int,int> progress,Action validate,Action submitting,Func<string,IReadOnlyList<long>,CancellationToken,Task> send)
    {
        if(pages.PageCount<1 || pages.PageCount>10000)throw new FormatException("Choose 1–10,000 stickers.");
        var file=Path.Combine(Path.GetTempPath(),"LableStudio-"+Guid.NewGuid().ToString("N")+".zpl");
        var lengths=new List<long>(pages.PageCount);var previous=0L;
        void Check(){cancellation.ThrowIfCancellationRequested();validate();}
        try
        {
            using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(output,Encoding.ASCII,64*1024))
            {
                for(var i=0;i<pages.PageCount;i++)
                {
                    Check();using var page=pages.GetPage(i);
                    if(page.Visual is not LabelView view)throw new InvalidOperationException("The sticker could not be rendered.");
                    ZebraRasterizer.Render(view,zebra).WriteZpl(writer);writer.Flush();
                    if(output.Length>512L*1024*1024)throw new InvalidOperationException("This batch is too large. Print a smaller range.");
                    lengths.Add(output.Position-previous);previous=output.Position;progress(i+1,pages.PageCount);
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            Check();submitting();await send(file,lengths,cancellation);
        }
        finally{try{File.Delete(file);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){AppDiagnostics.Log("Remove temporary print file",ex);}}
    }
}
