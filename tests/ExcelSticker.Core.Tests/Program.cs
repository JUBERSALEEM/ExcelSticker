using System.Globalization;
using ExcelSticker.Core;

var passed = 0;
void Check(string name, Action test)
{
    test(); passed++; Console.WriteLine($"PASS {name}");
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected '{expected}', got '{actual}'.");
}
void Invalid(Action action, string messagePart)
{
    try { action(); }
    catch (FormatException ex) when (ex.Message.Contains(messagePart, StringComparison.OrdinalIgnoreCase)) { return; }
    throw new Exception($"Expected a validation error containing '{messagePart}'.");
}
GlassLabel Make(string qty = "1", string width = "316.0", string height = "800.0", string order = "7494/71", string customer = "FUJSENG")
    => GlassLabel.Create(order, qty, "6MM CLEAR LOW E", "W6* F5", width, height, customer, "");
string Csv(string row) => LabelCsv.Header + "\r\n" + row;
const string sample = "7494/71,1,6MM CLEAR LOW E,W6* F5,316.0,800.0,FUJSENG,";

Check("manual entry matches reference", () => { var label = Make(); Equal("316.0 × 800.0", label.Size); Equal(1, label.Qty); });
Check("text is trimmed", () => Equal("7494/71", Make(order: " 7494/71 ").OrderNo));
Check("quantity lower bound", () => Invalid(() => Make(qty: "0"), "Qty"));
Check("quantity upper bound", () => Invalid(() => Make(qty: "10001"), "Qty"));
Check("fractional quantity rejected", () => Invalid(() => Make(qty: "1.5"), "Qty"));
Check("negative dimension rejected", () => Invalid(() => Make(width: "-1"), "Width"));
Check("zero dimension rejected", () => Invalid(() => Make(height: "0"), "Height"));
Check("huge dimension rejected", () => Invalid(() => Make(width: "100001"), "Width"));
Check("nonfinite dimension rejected", () => Invalid(() => Make(width: "NaN"), "Width"));
Check("required order", () => Invalid(() => Make(order: "  "), "Order No"));
Check("required customer", () => Invalid(() => Make(customer: ""), "Customer Name"));
Check("long text rejected", () => Invalid(() => Make(order: new string('x', 41)), "40"));
Check("control characters rejected", () => Invalid(() => Make(order: "one\ntwo"), "control"));
Check("comma decimals rejected", () => Invalid(() => Make(width: "316,0"), "dot"));
Check("CSV imports optional blank fields", () => { var rows = LabelCsv.Import(Csv(sample)); Equal(1, rows.Count); Equal("", rows[0].JobNo); });
Check("UTF-8 BOM and blank records", () => Equal(1, LabelCsv.Import("\uFEFF" + Csv(sample) + "\r\n\r\n,,,,,,,\r\n").Count));
Check("quoted comma and escaped quotes", () =>
{
    var label = LabelCsv.Import(Csv("A,2,\"CLEAR, LOW E\",\"W6\"\" F5\",100,70,Customer,Job"))[0];
    Equal("CLEAR, LOW E", label.GlassType); Equal("W6\" F5", label.ReferenceNo); Equal("Job", label.JobNo);
});
Check("headers can be reordered and spaced", () =>
{
    var label = LabelCsv.Import("job no,customer name,height,width,reference no,glass type,qty,order no\nJ,C,70,100,R,G,2,A")[0];
    Equal("A", label.OrderNo); Equal(100m, label.Width); Equal(2, label.Qty);
});
Check("Unicode text preserved", () => Equal("زجاج", LabelCsv.Import(Csv("A,1,زجاج,,100,70,عميل,"))[0].GlassType));
Check("empty file rejected", () => Invalid(() => LabelCsv.Import(""), "empty"));
Check("template without data rejected", () => Invalid(() => LabelCsv.Import(LabelCsv.Header + "\n"), "no labels"));
Check("missing heading rejected", () => Invalid(() => LabelCsv.Import("OrderNo,Qty\nA,1"), "Missing"));
Check("duplicate heading rejected", () => Invalid(() => LabelCsv.Import(LabelCsv.Header + ",Qty\n"), "unique"));
Check("wrong field count rejected", () => Invalid(() => LabelCsv.Import(Csv("A,1")), "record 2"));
Check("unclosed quote rejected", () => Invalid(() => LabelCsv.Import(Csv("\"A,1")), "unclosed"));
Check("embedded unescaped quote rejected", () => Invalid(() => LabelCsv.Import(Csv("A\"B,1")), "beginning"));
Check("text after closing quote rejected", () => Invalid(() => LabelCsv.Import(Csv("\"A\"B,1")), "closing"));
Check("multiline quoted field parsed then rejected by label validation", () => Invalid(() => LabelCsv.Import(Csv("\"A\nB\",1,G,,100,70,C,")), "control"));
Check("invalid second data row rejects whole import", () => Invalid(() => LabelCsv.Import(Csv(sample + "\nA,0,G,,100,70,C,")), "record 3"));
Check("dot decimals independent of current culture", () =>
{
    var original = CultureInfo.CurrentCulture;
    try { CultureInfo.CurrentCulture = new CultureInfo("de-DE"); Equal("316.0 × 800.0", LabelCsv.Import(Csv(sample))[0].Size); }
    finally { CultureInfo.CurrentCulture = original; }
});
Check("batch size limit", () => Invalid(() => LabelCsv.Import(Csv(string.Join("\n", Enumerable.Repeat(sample, LabelCsv.MaxLabels + 1)))), "at most"));
Console.WriteLine($"All {passed} core checks passed.");
