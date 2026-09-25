using System.Globalization;
using ClosedXML.Excel;
using SpecConvert.Core.Models;

namespace SpecConvert.Core.Services;

public sealed class ExcelExporter
{
    public static readonly string[] Headers = ["Поз", "Наименование и техническая характеристика", "Тип, марка, обозначение документа, опросного листа", "Код продукции", "Поставщик", "Ед. измерения", "Кол.", "Масса 1 ед., кг", "Примечание"];
    public void Export(string path, IEnumerable<SpecificationItem> rows)
    {
        if (!Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Выберите выходной файл .xlsx.");
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Спецификация");
        for (int i = 0; i < Headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = Headers[i];
            sheet.Cell(2, i + 1).Value = i + 1;
        }
        int row = 3;
        int position = 0;
        foreach (var item in rows)
        {
            string outputPosition = OutputRowFormatter.NextPosition(item, ref position);
            string outputName = TextNormalizer.Name(item.Name);
            object?[] values = item.RowType == SpecificationRowType.SectionHeader
                ? [null, outputName, null, null, null, null, null, null, null]
                : [outputPosition, outputName, item.TypeMark, item.ProductCode, item.Supplier, item.Unit, item.Quantity, item.UnitWeightKg, item.Note];
            for (int col = 0; col < values.Length; col++)
            {
                if (values[col] is null) continue;
                var cell = sheet.Cell(row, col + 1);
                if (values[col] is double d) cell.Value = d;
                else if (values[col] is decimal dec) cell.Value = (double)dec;
                else if (col is 6 or 7 && double.TryParse(values[col]!.ToString()!.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number)) cell.Value = number;
                else cell.Value = TextNormalizer.Normalize(values[col]!.ToString());
                if (col is 6 or 7 && cell.DataType == XLDataType.Number)
                {
                    double numericValue = cell.GetDouble();
                    cell.Style.NumberFormat.Format = numericValue == Math.Truncate(numericValue)
                        ? "0"
                        : "0.###############";
                }
            }
            if (item.RowType == SpecificationRowType.SectionHeader)
            { sheet.Range(row, 1, row, 9).Style.Font.Bold = true; sheet.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("E7EDF5"); }
            row++;
        }
        double[] widths = [12, 65, 43, 21, 27, 13, 12, 15, 35];
        for (int col = 1; col <= 9; col++) sheet.Column(col).Width = widths[col - 1];
        var range = sheet.Range(1, 1, row - 1, 9);
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Font.FontName = "Calibri"; range.Style.Font.FontSize = 11;
        sheet.Range(1, 1, 2, 9).Style.Font.Bold = true;
        sheet.Range(1, 1, 2, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("D5E2F2");
        sheet.Range(2, 1, 2, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(2, 1, 2, 9).Style.NumberFormat.Format = "0";
        for (int r = 1; r < row; r++)
        {
            double lines = Enumerable.Range(1, 9).Max(c => Math.Ceiling(sheet.Cell(r, c).GetString().Length / (widths[c - 1] * .85)));
            sheet.Row(r).Height = Math.Clamp(lines * 16 + 8, 26, 409);
        }
        sheet.SheetView.FreezeRows(2);
        // The numbering row is the filter header so it is never sorted with data.
        sheet.Range(2, 1, row - 1, 9).SetAutoFilter();
        // Save beside the destination, then replace atomically; a failed save leaves the old file intact.
        string full = Path.GetFullPath(path), temp = Path.Combine(Path.GetDirectoryName(full)!, $".specconvert-{Guid.NewGuid():N}.xlsx");
        try { book.SaveAs(temp); File.Move(temp, full, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
