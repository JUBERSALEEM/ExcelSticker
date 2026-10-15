using System.Net;
using ExcelSticker.Core;

internal static class PrinterNetworkChecks
{
    private static void Assert(bool value,string why){if(!value)throw new Exception(why);}
    private static void Reject(Action action){try{action();}catch(FormatException){return;}throw new Exception("Expected invalid network destination");}
    public static void Run(Action<string,Action> check)
    {
        check("Printer IP input accepts only explicit private IPv4, never hostnames or public Internet",()=>
        {
            foreach(var ip in new[]{"10.2.3.4","172.16.1.1","172.31.5.8","192.168.1.50","169.254.10.10"})Assert(PrinterNetwork.ParseAddress(ip).ToString()==ip,"Private address accepted");
            foreach(var ip in new[]{"8.8.8.8","127.0.0.1","0.0.0.0","172.32.1.1","192.169.1.1","localhost","192.168.1","0xC0A80101","3232235777","::1","192.168.1.50:9100","192.168.1.999","192.168.1.1\r\n^XA"})Reject(()=>PrinterNetwork.ParseAddress(ip));
        });
        check("LAN scan stays in the interface subnet and nearby /24, excluding self and broadcast",()=>
        {
            var ip=IPAddress.Parse("192.168.5.140");
            var hosts=PrinterNetwork.Nearby(ip,IPAddress.Parse("255.255.255.128")).Select(a=>a.ToString()).ToArray();
            Assert(hosts.Length==125 && hosts.All(a=>a.StartsWith("192.168.5.")) && !hosts.Contains("192.168.5.128") && !hosts.Contains("192.168.5.255") && !hosts.Contains(ip.ToString()),"Only other /25 hosts");
            Assert(PrinterNetwork.Nearby(ip,IPAddress.Parse("255.255.255.255")).Count()==0,"No /32 neighbors");
            Assert(PrinterNetwork.Nearby(IPAddress.Parse("10.4.7.8"),IPAddress.Parse("255.0.0.0")).Count()==253,"Large subnets do not cause whole-network scanning");
            Assert(!PrinterNetwork.Nearby(IPAddress.Parse("8.8.8.8"),IPAddress.Parse("255.255.255.0")).Any(),"Public NIC addresses are not scanned");
        });
        check("Direct network output requires an identified compatible ZT411 and stable device identity",()=>
        {
            var identity=new NetworkPrinterIdentity("ZT411",203,"epl_zpl","SERIAL123");
            Assert(identity.Profile().DotsPerMm==8,"Compatible bilingual ZT411");
            Reject(()=> (identity with {Model="Unknown port 9100 service"}).Profile());
            Reject(()=> (identity with {Language="epl"}).Profile());
            Reject(()=> (identity with {Language="not_zpl"}).Profile());
            Reject(()=> (identity with {Serial="?"}).Profile());
            Reject(()=> (identity with {Dpi=1200}).Profile());
            Reject(()=> (identity with {Model="ZT411-203dpi",Dpi=0}).Profile());
            Assert(!identity.SameDevice(identity with {Serial="OTHER"}) && !identity.SameDevice(identity with {Dpi=300}),"Changed IP identity/head cannot silently receive a saved job");
        });
    }
}
