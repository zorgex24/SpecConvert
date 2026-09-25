namespace SpecConvert.Core.Models;

public sealed record SourceCell(int Row, int Column, object? Value, bool HasFormula = false);
public sealed record MergedArea(int FirstRow, int FirstColumn, int LastRow, int LastColumn)
{
    public bool Contains(int row, int col) => row >= FirstRow && row <= LastRow && col >= FirstColumn && col <= LastColumn;
}
public sealed class SourceSheet(string name)
{
    public string Name { get; } = name;
    public Dictionary<(int Row, int Column), SourceCell> Cells { get; } = [];
    public List<MergedArea> Merges { get; } = [];
    public int LastRow => Cells.Count == 0 ? 0 : Cells.Keys.Max(k => k.Row);
}
public sealed record ColumnBand(int First, int Last);
public sealed record SourceLayout(int HeaderRow, int HeaderEndRow, ColumnBand?[] Columns);
public sealed record PhysicalRow(string Sheet, int Row, object?[] Values)
{
    public string Text(int index) => Services.TextNormalizer.Normalize(Values[index]?.ToString());
    public bool HasQuantity => Values[6] is not null && Text(6).Length > 0;
}
