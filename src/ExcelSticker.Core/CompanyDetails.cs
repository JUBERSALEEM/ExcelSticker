namespace ExcelSticker.Core;

/// <summary>Editable company data saved atomically with the three-format library.</summary>
public sealed class CompanyDetails
{
    public string Name {get;set;}="";
    public string Address {get;set;}="";
    public string Phone {get;set;}="";
    public string Website {get;set;}="";
    public string Email {get;set;}="";
    public CompanyDetails Clone()=>(CompanyDetails)MemberwiseClone();
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Name))throw new FormatException("Enter your company name. Other company details can be blank.");
        foreach(var (label,value,limit) in new[]{("Company name",Name,120),("Address",Address,240),("Phone",Phone,80),("Website",Website,160),("Email",Email,160)})
            if(value==null || value.Length>limit || value.Any(char.IsControl))throw new FormatException($"{label} must be one line of at most {limit} characters.");
    }
}
