using System.Globalization;
using SpecConvert.Core.Models;

namespace SpecConvert.Core.Services;

public static class OutputRowFormatter
{
    public static string NextPosition(SpecificationItem item, ref int number) =>
        item.RowType == SpecificationRowType.Item && !string.IsNullOrWhiteSpace(item.Quantity?.ToString())
            ? (++number).ToString(CultureInfo.InvariantCulture)
            : "";

    public static void Apply(IEnumerable<SpecificationItem> rows)
    {
        int number = 0;
        foreach (var row in rows)
        {
            row.Position = NextPosition(row, ref number);
            row.Name = TextNormalizer.Name(row.Name);
        }
    }
}
