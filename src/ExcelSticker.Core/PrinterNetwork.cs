using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace ExcelSticker.Core;

public static class PrinterNetwork
{
    public static bool IsPrivate(IPAddress address)
    {
        if(address.AddressFamily!=AddressFamily.InterNetwork)return false;
        var b=address.GetAddressBytes();
        return b[0]==10 || b[0]==172 && b[1]>=16 && b[1]<=31 || b[0]==192 && b[1]==168 || b[0]==169 && b[1]==254;
    }
    public static IPAddress ParseAddress(string? value)
    {
        var parts=(value?.Trim()??"").Split('.');
        if(parts.Length!=4 || parts.Any(p=>p.Length==0 || p.Length>3 || p.Any(c=>c<'0'||c>'9') || !byte.TryParse(p,NumberStyles.None,CultureInfo.InvariantCulture,out _)))
            throw new FormatException("Enter the printer's local IPv4 address, for example 192.168.1.50.");
        var address=new IPAddress(parts.Select(p=>byte.Parse(p,CultureInfo.InvariantCulture)).ToArray());
        if(!IsPrivate(address))throw new FormatException("Use a private LAN or VPN printer address. Public Internet addresses are not scanned or printed to.");
        return address;
    }
    public static IEnumerable<IPAddress> Nearby(IPAddress address,IPAddress mask)
    {
        if(!IsPrivate(address) || mask.AddressFamily!=AddressFamily.InterNetwork)yield break;
        var a=address.GetAddressBytes();var m=mask.GetAddressBytes();
        for(var last=1;last<255;last++)
        {
            var b=(byte[])a.Clone();b[3]=(byte)last;
            if(b.SequenceEqual(a) || Enumerable.Range(0,4).Any(i=>(b[i]&m[i])!=(a[i]&m[i])))continue;
            if(Enumerable.Range(0,4).All(i=>(b[i]&~m[i])==0) || Enumerable.Range(0,4).All(i=>(b[i]|m[i])==255))continue;
            yield return new IPAddress(b);
        }
    }
}

public sealed record NetworkPrinterIdentity(string Model,int Dpi,string Language,string Serial)
{
    public ZebraZt411 Profile()
    {
        if(string.IsNullOrWhiteSpace(Model) || string.IsNullOrWhiteSpace(Language))throw new FormatException("The printer identity is incomplete. Check the IP again.");
        if(Dpi is not (200 or 203 or 300 or 305 or 600 or 610))throw new FormatException("The printer did not report a supported resolution.");
        var profile=ZebraZt411.Detect(Model,Dpi)??throw new FormatException("This device was not identified as a supported ZT411. Install its Windows driver and select its Windows printer instead.");
        if(Language.ToLowerInvariant() is not ("zpl" or "epl_zpl" or "hybrid_xml_zpl" or "ezpl"))
            throw new FormatException("This ZT411 is not accepting ZPL. Enable ZPL on the printer, or use its Windows driver.");
        if(string.IsNullOrWhiteSpace(Serial) || Serial=="?")throw new FormatException("The printer did not report its identity. Use its Windows printer instead.");
        return profile;
    }
    public bool SameDevice(NetworkPrinterIdentity other)=>Model==other.Model && Dpi==other.Dpi && Serial==other.Serial;
}
