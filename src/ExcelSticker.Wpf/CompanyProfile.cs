using System.IO;
using System.Text.Json;

namespace ExcelSticker.Wpf;

public sealed class CompanyProfile
{
    public string Name { get; set; } = "Ocean Glass Industry LLC";
    public string Address { get; set; } = "Add.: Industrial Area Jurf-3 Ajman UAE";
    public string Phone { get; set; } = "Tel. 085205731";
    public string Website { get; set; } = "Web: www.oceanglassind.com";
    public string Email { get; set; } = "Mail: Oceanglass.1511@gmail.com";

    public static CompanyProfile Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "company.json");
        if (!File.Exists(path)) return new CompanyProfile();
        var company = JsonSerializer.Deserialize<CompanyProfile>(File.ReadAllText(path))
            ?? throw new FormatException("company.json must contain a company profile.");
        if (string.IsNullOrWhiteSpace(company.Name)) throw new FormatException("The company name cannot be blank.");
        return company;
    }
}
