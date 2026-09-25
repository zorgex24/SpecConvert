using SpecConvert.Core.Models;

namespace SpecConvert.Core.Services;

public sealed class SourceLayoutDetector
{
    public static int HeaderIndex(string text)
    {
        var s = TextNormalizer.Header(text);
        if (s is "поз" or "позиция") return 0;
        if (s.StartsWith("наименование")) return 1;
        if (s.StartsWith("типмарка") || s == "тип") return 2;
        if (s.StartsWith("кодпродукции")) return 3;
        if (s == "поставщик") return 4;
        if (s.StartsWith("едизмер") || s == "ед") return 5;
        if (s is "кол" or "количество") return 6;
        if (s.StartsWith("масса")) return 7;
        if (s == "примечание") return 8;
        return -1;
    }

    public List<SourceLayout> Detect(SourceSheet sheet)
    {
        var result = new List<SourceLayout>();
        foreach (var row in sheet.Cells.Values.GroupBy(c => c.Row).OrderBy(g => g.Key))
        {
            var matches = row.Select(c => (Cell: c, Index: HeaderIndex(c.Value?.ToString() ?? ""))).Where(x => x.Index >= 0).ToList();
            if (!matches.Any(x => x.Index == 1) || !matches.Any(x => x.Index == 6) || matches.Count < 3) continue;
            var columns = new ColumnBand?[9];
            int end = row.Key;
            foreach (var (cell, index) in matches)
            {
                var merge = sheet.Merges.FirstOrDefault(m => m.Contains(cell.Row, cell.Column));
                columns[index] = new(cell.Column, merge?.LastColumn ?? cell.Column);
                end = Math.Max(end, merge?.LastRow ?? cell.Row);
            }
            result.Add(new(row.Key, end, columns));
        }
        return result;
    }
}
