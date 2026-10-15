using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

/// <summary>Read-only identity queries and optional explicit ZPL submission on the SAME TCP connection.</summary>
internal sealed class NetworkPrinterConnection : IDisposable
{
    private readonly TcpClient _client=new();
    public NetworkPrinterIdentity Identity {get;private set;}=null!;
    private NetworkStream Stream=>_client.GetStream();
    public static async Task<NetworkPrinterConnection> OpenAsync(string address,CancellationToken cancellation)
    {
        var connection=new NetworkPrinterConnection();
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(12));
            await connection._client.ConnectAsync(PrinterNetwork.ParseAddress(address),9100,timeout.Token);
            var model=await connection.Query("device.product_name",timeout.Token);
            if(!ZebraZt411.IsDriver(model))throw new FormatException("This device did not identify as a ZT411. Use its installed Windows printer instead.");
            var dpi=await connection.Query("head.resolution.in_dpi",timeout.Token);
            var language=await connection.Query("device.languages",timeout.Token);
            var serial=await connection.Query("device.unique_id",timeout.Token);
            if(!int.TryParse(dpi,NumberStyles.None,CultureInfo.InvariantCulture,out var resolution))throw new FormatException("The printer did not report a supported resolution.");
            connection.Identity=new NetworkPrinterIdentity(model,resolution,language,serial);connection.Identity.Profile();return connection;
        }
        catch(OperationCanceledException ex) when(!cancellation.IsCancellationRequested){connection.Dispose();throw new TimeoutException("The network printer did not respond within 12 seconds. Check its IP address and connection.",ex);}
        catch{connection.Dispose();throw;}
    }
    private async Task<string> Query(string setting,CancellationToken cancellation)
    {
        // Only fixed, read-only SGD queries from this class. Never interpolate user text in commands.
        await Stream.WriteAsync(Encoding.ASCII.GetBytes($"! U1 getvar \"{setting}\"\r\n"),cancellation);
        var bytes=new byte[1];var response=new StringBuilder();var opened=false;
        for(var i=0;i<512;i++)
        {
            if(await Stream.ReadAsync(bytes,cancellation)==0)throw new IOException("The printer closed the connection before identifying itself.");
            var c=(char)bytes[0];
            if(!opened){if(c=='"'){opened=true;continue;}if(c is '\r' or '\n' or ' ')continue;throw new FormatException("The device did not return a Zebra identity response.");}
            if(c=='"')return response.ToString().Trim();
            if(char.IsControl(c))throw new FormatException("Invalid printer identity response.");
            response.Append(c);
        }
        throw new FormatException("The printer identity response was too long.");
    }
    public async Task SendFileAsync(string file,CancellationToken cancellation)
    {
        using var input=File.OpenRead(file);var buffer=new byte[64*1024];int count;
        while((count=await input.ReadAsync(buffer,cancellation))>0)
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try{await Stream.WriteAsync(buffer.AsMemory(0,count),timeout.Token);}
            catch(OperationCanceledException ex) when(!cancellation.IsCancellationRequested){throw new TimeoutException("The printer stopped accepting data. Some stickers may already have printed.",ex);}
        }
    }
    public void Dispose()=>_client.Dispose();
}
