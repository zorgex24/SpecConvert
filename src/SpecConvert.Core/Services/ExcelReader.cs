using ClosedXML.Excel;
using SpecConvert.Core.Models;

namespace SpecConvert.Core.Services;

public sealed class ExcelReader
{
    public List<SourceSheet> Read(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Поддерживаются только файлы .xlsx.");
        // Own the stream so even a failing workbook constructor releases the input file.
        using var input = File.OpenRead(path);
        using var workbook = new XLWorkbook(input);
        var sheets = new List<SourceSheet>();
        foreach (var ws in workbook.Worksheets)
        {
            var sheet = new SourceSheet(ws.Name);
            foreach (var range in ws.MergedRanges)
            {
                var a = range.RangeAddress;
                sheet.Merges.Add(new(a.FirstAddress.RowNumber, a.FirstAddress.ColumnNumber, a.LastAddress.RowNumber, a.LastAddress.ColumnNumber));
            }
            foreach (var cell in ws.CellsUsed(XLCellsUsedOptions.Contents))
            {
                var value = cell.HasFormula ? cell.CachedValue : cell.Value;
                // Do not run arbitrary or unsupported Excel formulas. Use the saved calculation cache.
                object? result = value.Type switch
                {
                    XLDataType.Blank => null,
                    XLDataType.Number => value.GetNumber(),
                    _ => TextNormalizer.Normalize(value.ToString())
                };
                if (cell.HasFormula && value.Type == XLDataType.Blank) result = "[Нет кэша формулы: " + cell.FormulaA1 + "]";
                sheet.Cells[(cell.Address.RowNumber, cell.Address.ColumnNumber)] = new(cell.Address.RowNumber, cell.Address.ColumnNumber, result, cell.HasFormula);
            }
            sheets.Add(sheet);
        }
        return sheets;
    }
}
