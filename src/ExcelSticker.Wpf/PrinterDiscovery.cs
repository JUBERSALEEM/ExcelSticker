using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ExcelSticker.Core;

namespace ExcelSticker.Wpf;

public sealed record PrinterDestination(string Kind,string Name,string Connection,string Status,NetworkPrinterIdentity? Identity=null)
{
    public string Key=>Kind+":"+Name;
    public string Caption=>Name+" · "+Connection;
}

/// <summary>Discovery only. No print data is sent by either scan.</summary>
public sealed class PrinterDiscovery
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct PrinterInfo2
    {
        public nint ServerName,PrinterName,ShareName,PortName,DriverName,Comment,Location,DevMode,SepFile,PrintProcessor,Datatype,Parameters,SecurityDescriptor;
        public uint Attributes,Priority,DefaultPriority,StartTime,UntilTime,Status,Jobs,AveragePpm;
    }
    [DllImport("winspool.drv",EntryPoint="EnumPrintersW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool EnumPrinters(uint flags,string? name,uint level,nint buffer,uint size,out uint needed,out uint returned);
    private readonly object _gate=new();
    private Task<IReadOnlyList<PrinterDestination>>? _scan;
    public Task<IReadOnlyList<PrinterDestination>> WindowsAsync()
    {
        // A timed-out caller must not spawn duplicate native RPC scans that are still running.
        lock(_gate)return _scan is {IsCompleted:false}?_scan:(_scan=Task.Run(Windows));
    }
    private static IReadOnlyList<PrinterDestination> Windows()
    {
        const uint flags=2|4; // Local installed queues and connected shared queues; not all Internet printers.
        var ok=EnumPrinters(flags,null,2,0,0,out var needed,out _);
        if(!ok && Marshal.GetLastWin32Error()!=122)throw new Win32Exception(Marshal.GetLastWin32Error());
        if(needed==0)return Array.Empty<PrinterDestination>();
        for(var attempt=0;attempt<3;attempt++)
        {
            if(needed>32*1024*1024)throw new InvalidOperationException("Windows returned too much printer information.");
            var size=needed;var buffer=Marshal.AllocHGlobal(checked((int)size));
            try
            {
                if(!EnumPrinters(flags,null,2,buffer,size,out needed,out var count))
                {var error=Marshal.GetLastWin32Error();if(error==122)continue;throw new Win32Exception(error);}
                var stride=Marshal.SizeOf<PrinterInfo2>();
                if((long)count*stride>size)throw new InvalidOperationException("Windows returned incomplete printer information.");
                var result=new List<PrinterDestination>();
                for(var i=0;i<count;i++)
                {
                    var info=Marshal.PtrToStructure<PrinterInfo2>(buffer+i*stride);
                    var name=Marshal.PtrToStringUni(info.PrinterName)??"";if(name.Length==0)continue;
                    var port=Marshal.PtrToStringUni(info.PortName)??"";var driver=Marshal.PtrToStringUni(info.DriverName)??"";
                    var connection=port.StartsWith("USB",StringComparison.OrdinalIgnoreCase)?"USB":name.StartsWith(@"\\",StringComparison.Ordinal)||port.StartsWith("IP_",StringComparison.OrdinalIgnoreCase)||port.StartsWith("WSD",StringComparison.OrdinalIgnoreCase)||IPAddress.TryParse(port,out _)?"Network / Windows":"Windows";
                    var state=(info.Status&0x80)!=0?"Windows reports offline":(info.Status&1)!=0?"Queue paused":(info.Status&0x10)!=0?"Paper out reported":"Installed; physical readiness is not verified";
                    result.Add(new PrinterDestination("windows",name,connection,$"{driver} · {port} · {state}"));
                }
                return result.DistinctBy(p=>p.Key).OrderBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).ToArray();
            }
            finally{Marshal.FreeHGlobal(buffer);}
        }
        throw new InvalidOperationException("The printer list changed during discovery. Refresh printers again.");
    }
    public static IPAddress[] LocalCandidates()=>NetworkInterface.GetAllNetworkInterfaces()
        .Where(n=>n.OperationalStatus==OperationalStatus.Up && n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
        .SelectMany(n=>n.GetIPProperties().UnicastAddresses)
        .Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork)
        .SelectMany(a=>PrinterNetwork.Nearby(a.Address,a.IPv4Mask)).Distinct().Take(1024).ToArray();
    public static async Task ScanLanAsync(Action<string> found,CancellationToken cancellation)
    {
        var addresses=LocalCandidates();
        if(addresses.Length==0)throw new InvalidOperationException("No private wired/Wi-Fi network was found. Enter the printer's LAN or VPN address instead.");
        await Parallel.ForEachAsync(addresses,new ParallelOptions {MaxDegreeOfParallelism=24,CancellationToken=cancellation},async(address,token)=>
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(400);
            using var client=new TcpClient();
            try{await client.ConnectAsync(address,9100,timeout.Token);found(address.ToString());}
            catch(Exception ex) when(ex is SocketException or OperationCanceledException){ }
        });
    }
}
