using SpecConvert.Core.Models;

namespace SpecConvert.Core.Services;

public sealed class CellValueResolver(SourceSheet sheet)
{
    public SourceCell? Resolve(int row, int column)
    {
        var merged = sheet.Merges.FirstOrDefault(m => m.Contains(row, column));
        return sheet.Cells.GetValueOrDefault(merged is null ? (row, column) : (merged.FirstRow, merged.FirstColumn));
    }

    public object? ReadBand(int row, ColumnBand? band)
    {
        if (band is null) return null;
        var values = new List<object>();
        var seen = new HashSet<(int, int)>();
        for (int col = band.First; col <= band.Last; col++)
        {
            var cell = Resolve(row, col);
            if (cell is null || cell.Row != row || cell.Column < band.First || !seen.Add((cell.Row, cell.Column))) continue;
            if (cell.Value is not null && TextNormalizer.Normalize(cell.Value.ToString()).Length > 0) values.Add(cell.Value);
        }
        return values.Count switch { 0 => null, 1 => values[0], _ => TextNormalizer.Join(values.Select(v => v.ToString()!)) };
    }
}
