using System.IO;
using System.Text.Json;
using System.Windows.Documents;

namespace ExcelSticker.Wpf;

/// <summary>Explicit destination + immutable pages. No discovery method or selection method prints.</summary>
public sealed class PrinterService
{
    private readonly StickerPrinter _windows;
    private readonly string _path=Path.Combine(AppDiagnostics.DataDirectory,"print-destination.json");
    public PrinterDestination? Selected {get;private set;}
    public PrinterDestination? Remembered {get;private set;}
    public PrinterService(StickerPrinter windows)
    {
        _windows=windows;
        try
        {
            if(File.Exists(_path))Remembered=JsonSerializer.Deserialize<PrinterDestination>(File.ReadAllText(_path));
            else if(windows.PreferredQueueName is {} name)Remembered=new PrinterDestination("windows",name,"Windows","Previously selected");
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException){AppDiagnostics.Log("Read print destination",ex);}
    }
    public void Select(PrinterDestination? printer)
    {
        if(printer!=null && printer.Kind is not ("windows" or "network"))throw new ArgumentException("Unknown printer connection.");
        if(printer is {Kind:"network"})
        {
            ExcelSticker.Core.PrinterNetwork.ParseAddress(printer.Name);
            (printer.Identity??throw new ArgumentException("Check the network printer before selecting it.")).Profile();
        }
        Selected=printer;if(printer==null)return;Remembered=printer;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path+".tmp",JsonSerializer.Serialize(printer));File.Move(_path+".tmp",_path,true);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){AppDiagnostics.Log("Remember print destination",ex);}
    }
    public static async Task<PrinterDestination> CheckNetworkAsync(string address,CancellationToken cancellation)
    {
        var ip=ExcelSticker.Core.PrinterNetwork.ParseAddress(address).ToString();
        using var connection=await NetworkPrinterConnection.OpenAsync(ip,cancellation);
        var identity=connection.Identity;
        return new PrinterDestination("network",ip,$"{identity.Model} · LAN",$"Responded · {identity.Dpi} dpi · {identity.Language} · ID {identity.Serial}",identity);
    }
    public async Task<string> SendAsync(DocumentPaginator pages,CancellationToken cancellation,Action<int,int> progress,Action validate,Action submitting)
    {
        var printer=Selected??throw new InvalidOperationException("Select a printer above the sticker first.");
        void Check(){cancellation.ThrowIfCancellationRequested();validate();if(!ReferenceEquals(printer,Selected))throw new InvalidOperationException("Printer selection changed. Nothing further was sent.");}
        Check();
        if(printer.Kind=="windows")
        {
            _windows.SelectQueue(printer.Name,pages.PageSize);
            return await _windows.SendAsync(pages,cancellation,progress,Check,submitting);
        }
        var identity=printer.Identity??throw new InvalidOperationException("Check the network printer first.");
        await ZebraPrintJob.SendAsync(pages,identity.Profile(),cancellation,progress,Check,()=>{},async(file,_,token)=>
        {
            // Render first; do not leave a TCP printer connection idle while a large batch is prepared.
            // Identify and submit on one connection, so a new device at a saved DHCP address is not trusted.
            using var connection=await NetworkPrinterConnection.OpenAsync(printer.Name,token);
            if(!identity.SameDevice(connection.Identity))throw new InvalidOperationException("The device at this address changed. Check and select the printer again.");
            Check();submitting();await connection.SendFileAsync(file,token);
        });
        return printer.Caption;
    }
}
