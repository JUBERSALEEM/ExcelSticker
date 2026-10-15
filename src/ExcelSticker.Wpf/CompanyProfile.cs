using System.IO;
using ExcelSticker.Core;
using System.Text.Json;

namespace ExcelSticker.Wpf;

public sealed class CompanyProfile
{
    public string Name { get; set; } = "Ocean Glass Industry LLC";
    public string Address { get; set; } = "Add.: Industrial Area Jurf-3 Ajman UAE";
    public string Phone { get; set; } = "Tel. 085205731";
    public string Website { get; set; } = "Web: www.oceanglassind.com";
    public string Email { get; set; } = "Mail: Oceanglass.1511@gmail.com";

    public CompanyProfile Clone()=>new() {Name=Name,Address=Address,Phone=Phone,Website=Website,Email=Email};

    public CompanyDetails ToDetails()=>new() {Name=Name,Address=Address,Phone=Phone,Website=Website,Email=Email};
    public static CompanyProfile FromDetails(CompanyDetails details)=>new() {Name=details.Name,Address=details.Address,Phone=details.Phone,Website=details.Website,Email=details.Email};

    public static CompanyProfile Load(string? directory=null)
    {
        var path = Path.Combine(directory??AppContext.BaseDirectory, "company.json");
        using var input=File.Exists(path)?File.OpenRead(path):typeof(CompanyProfile).Assembly.GetManifestResourceStream("LableStudio.CompanyDefaults.json")
            ?? throw new InvalidOperationException("The bundled company profile is missing.");
        var company = JsonSerializer.Deserialize<CompanyProfile>(input)
            ?? throw new FormatException("company.json must contain a company profile.");
        if (string.IsNullOrWhiteSpace(company.Name)) throw new FormatException("The company name cannot be blank.");
        return company;
    }
}
