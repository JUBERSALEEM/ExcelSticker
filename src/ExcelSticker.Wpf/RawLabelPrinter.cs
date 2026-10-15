using System.ComponentModel;
using ExcelSticker.Core;
using System.IO;
using System.Runtime.InteropServices;

namespace ExcelSticker.Wpf;

/// <summary>RAW ZPL only, never XPS. Runs off the UI thread; no retry after an uncertain spool failure.</summary>
internal static class RawLabelPrinter
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct DocumentInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string Name;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Output;
        [MarshalAs(UnmanagedType.LPWStr)] public string DataType;
    }
    [DllImport("winspool.drv",EntryPoint="OpenPrinterW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool OpenPrinter(string name,out nint handle,nint defaults);
    [DllImport("winspool.drv",EntryPoint="StartDocPrinterW",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern int StartDocPrinter(nint handle,int level,ref DocumentInfo info);
    [DllImport("winspool.drv",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool StartPagePrinter(nint handle);
    [DllImport("winspool.drv",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool WritePrinter(nint handle,nint buffer,int count,out int written);
    [DllImport("winspool.drv",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool EndPagePrinter(nint handle);
    [DllImport("winspool.drv",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool EndDocPrinter(nint handle);
    [DllImport("winspool.drv",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool AbortPrinter(nint handle);
    [DllImport("winspool.drv",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool ClosePrinter(nint handle);
    private static void Check(bool success){if(!success)throw new Win32Exception(Marshal.GetLastWin32Error());}
    public static void Send(string printer,string file,IReadOnlyList<long> pageLengths,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        Check(OpenPrinter(printer,out var handle,0));var started=false;var complete=false;
        try
        {
            var info=new DocumentInfo {Name="LABLE STUDIO stickers",Output=null,DataType="RAW"};
            if(StartDocPrinter(handle,1,ref info)==0)throw new Win32Exception(Marshal.GetLastWin32Error());
            started=true;
            using var input=File.OpenRead(file);var bytes=new byte[64*1024];var pinned=GCHandle.Alloc(bytes,GCHandleType.Pinned);
            try
            {
                foreach(var length in pageLengths)
                {
                    cancellation.ThrowIfCancellationRequested();Check(StartPagePrinter(handle));
                    LabelSpoolTransfer.Copy(input,bytes,(offset,count)=>
                    {
                        Check(WritePrinter(handle,pinned.AddrOfPinnedObject()+offset,count,out var written));return written;
                    },cancellation,length);
                    cancellation.ThrowIfCancellationRequested();Check(EndPagePrinter(handle));
                }
            }
            finally{pinned.Free();}
            cancellation.ThrowIfCancellationRequested();Check(EndDocPrinter(handle));complete=true;
        }
        finally{if(started&&!complete)AbortPrinter(handle);ClosePrinter(handle);}
    }
}
