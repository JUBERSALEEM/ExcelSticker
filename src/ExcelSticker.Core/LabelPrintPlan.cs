namespace ExcelSticker.Core;

public enum NumberingStyle { QuantityOnly, EntryCopy, CopyOfTotal }

public static class LabelPrintPlan
{
    public static GlassLabel[] Build(IEnumerable<GlassLabel> labels, bool repeatQuantity, NumberingStyle numbering)
    {
        if(!Enum.IsDefined(numbering)) throw new ArgumentOutOfRangeException(nameof(numbering));
        var source=labels.ToArray();
        if(source.Any(l=>l.Qty<1 || l.Qty>10000 || l.EntryNumber<1)) throw new FormatException("Invalid label quantity or entry number.");
        var count=source.Sum(l=>repeatQuantity ? (long)l.Qty : 1);
        if(count>10000) throw new FormatException("A print job is limited to 10,000 pages.");
        return source.SelectMany(label=>Enumerable.Range(1,repeatQuantity?label.Qty:1)
            .Select(copy=>label with { CopyIndex=copy, Numbering=numbering })).ToArray();
    }
}
