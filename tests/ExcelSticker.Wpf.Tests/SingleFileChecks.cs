using System.IO;
using System.Text.Json;
using ExcelSticker.Wpf;

internal static class SingleFileChecks
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    public static void Run()
    {
        var folder=Path.Combine(Path.GetTempPath(),"lablestudio-profile-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            using var resource=typeof(CompanyProfile).Assembly.GetManifestResourceStream("LableStudio.CompanyDefaults.json");
            Check(resource!=null,"Company defaults are an assembly resource, not a required publish sidecar");
            var embedded=JsonSerializer.Deserialize<CompanyProfile>(resource!)!;
            var fallback=CompanyProfile.Load(folder);
            Check(JsonSerializer.Serialize(embedded)==JsonSerializer.Serialize(fallback),"Missing adjacent JSON uses bundled company details, including customized build-time values");
            var path=Path.Combine(folder,"company.json");File.WriteAllText(path,"{\"Name\":\"Factory override\",\"Phone\":\"123\"}");
            var external=CompanyProfile.Load(folder);
            Check(external.Name=="Factory override" && external.Phone=="123","Optional adjacent company profile overrides the bundled default");
            File.WriteAllText(path,"{\"Name\":\" \"}");
            try{CompanyProfile.Load(folder);throw new Exception("Blank external company name was accepted");}
            catch(FormatException){Console.WriteLine("PASS Invalid adjacent company settings are not silently ignored");}
        }
        finally{Directory.Delete(folder,true);}
    }
}
