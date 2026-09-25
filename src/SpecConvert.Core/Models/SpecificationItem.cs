namespace SpecConvert.Core.Models;

public enum SpecificationRowType { Item, SectionHeader }

public sealed class SpecificationItem
{
    public SpecificationRowType RowType { get; set; }
    public string Position { get; set; } = "";
    public string Name { get; set; } = "";
    public string TypeMark { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string Unit { get; set; } = "";
    // Object preserves numeric Excel values and exceptional source text; the grid edits both.
    public object? Quantity { get; set; }
    public object? UnitWeightKg { get; set; }
    public string Note { get; set; } = "";
    public string SourceSheet { get; set; } = "";
    public int SourceRow { get; set; }
}

public sealed record ParseWarning(string Sheet, int Row, string Message)
{
    public override string ToString() => $"{Sheet}, строка {Row}: {Message}";
}

public sealed class ParseResult
{
    public List<SpecificationItem> Rows { get; } = [];
    public List<ParseWarning> Warnings { get; } = [];
    public List<string> Log { get; } = [];
    public int SheetsProcessed { get; set; }
    public int QuantityAnchors { get; set; }
    public int ItemCount => Rows.Count(r => r.RowType == SpecificationRowType.Item);
    public int SectionCount => Rows.Count - ItemCount;
    public bool IsValid => QuantityAnchors == ItemCount;
    public string Summary => $"Листов: {SheetsProcessed}; количеств: {QuantityAnchors}; позиций: {ItemCount}; разделов: {SectionCount}; предупреждений: {Warnings.Count}";
}
