using System.Text.RegularExpressions;
using SpecConvert.Core.Models;

namespace SpecConvert.Core.Services;

public sealed class SpecificationParser(ISectionClassifier? sections = null)
{
    private readonly ISectionClassifier sections = sections ?? new SectionClassifier();

    public ParseResult Parse(IReadOnlyList<SourceSheet> sheets, IProgress<string>? progress = null)
    {
        var result = new ParseResult();
        var block = new List<PhysicalRow>();
        void Flush() { EmitBlock(block, result); block.Clear(); }
        foreach (var sheet in sheets)
        {
            result.SheetsProcessed++;
            progress?.Report($"Чтение листа {sheet.Name} ({result.SheetsProcessed}/{sheets.Count})");
            var layouts = new SourceLayoutDetector().Detect(sheet);
            if (layouts.Count == 0)
            {
                result.Warnings.Add(new(sheet.Name, 1, "Таблица не распознана: отсутствует заголовок наименования или количества. Лист пропущен."));
                Flush();
                continue;
            }
            var resolver = new CellValueResolver(sheet);
            foreach (var layout in layouts)
            {
                result.Log.Add($"{sheet.Name}: заголовок {layout.HeaderRow}; графы {string.Join(", ", layout.Columns.Select(b => b is null ? "нет" : $"{b.First}:{b.Last}"))}");
                int limit = layouts.Where(l => l.HeaderRow > layout.HeaderRow).Select(l => l.HeaderRow - 1).DefaultIfEmpty(sheet.LastRow).Min();
                int firstCol = layout.Columns.OfType<ColumnBand>().Min(c => c.First);
                int lastCol = layout.Columns.OfType<ColumnBand>().Max(c => c.Last);
                for (int row = layout.HeaderEndRow + 1; row <= limit; row++)
                {
                    // Side notes outside the table are ignored. A nonempty merge crossing
                    // logical column boundaries identifies the drawing's title block.
                    bool crossed = sheet.Merges.Any(m => m.FirstRow == row && m.FirstColumn > layout.Columns[1]!.Last && m.LastColumn <= lastCol
                        && layout.Columns.OfType<ColumnBand>().Count(b => m.FirstColumn <= b.Last && m.LastColumn >= b.First) > 1
                        && sheet.Cells.TryGetValue((row, m.FirstColumn), out var c) && !string.IsNullOrWhiteSpace(c.Value?.ToString()));
                    var texts = sheet.Cells.Values.Where(c => c.Row == row && c.Column >= firstCol && c.Column <= lastCol).Select(c => TextNormalizer.Header(c.Value?.ToString())).ToHashSet();
                    bool stamp = texts.Contains("колуч") || (texts.Contains("изм") && texts.Contains("лист")) || texts.Contains("разраб");
                    if (crossed || stamp) { result.Log.Add($"{sheet.Name}: граница штампа {row}"); break; }
                    var physical = new PhysicalRow(sheet.Name, row, layout.Columns.Select(b => resolver.ReadBand(row, b)).ToArray());
                    if (physical.Values.All(v => v is null)) continue;
                    if (physical.HasQuantity) result.QuantityAnchors++;
                    if (sections.IsSection(physical))
                    {
                        Flush();
                        result.Rows.Add(new() { RowType = SpecificationRowType.SectionHeader, Name = physical.Text(1), SourceSheet = sheet.Name, SourceRow = row });
                        continue;
                    }
                    if (block.Count == 0 && result.Rows.LastOrDefault() is { RowType: SpecificationRowType.SectionHeader } section
                        && !physical.HasQuantity && physical.Text(0).Length == 0 && physical.Text(2).Length == 0
                        && (physical.Text(1).StartsWith('(') || section.Name.StartsWith("Подстанция укомплектовывается:")))
                    {
                        section.Name = TextNormalizer.Join([section.Name, physical.Text(1)]);
                        continue;
                    }
                    if (StartsBlock(physical, block)) Flush();
                    block.Add(physical);
                }
            }
        }
        Flush();
        foreach (var item in result.Rows.Where(r => r.RowType == SpecificationRowType.Item))
        {
            if (item.Name.Length == 0) Warn(result, item, "Не определено наименование.");
            if (item.Unit.Length == 0) Warn(result, item, "Не определена единица измерения.");
            if (item.TypeMark.Length == 0) Warn(result, item, "Не определен тип/марка.");
            if (item.Quantity is string) Warn(result, item, "Количество сохранено как текст. Проверьте значение.");
            if (new[] { item.Name, item.TypeMark, item.Note, item.Quantity?.ToString(), item.UnitWeightKg?.ToString() }.Any(t => t?.Contains("[Нет кэша формулы:") == true))
                Warn(result, item, "В исходнике нет сохраненного результата формулы. Пересчитайте исходную книгу и повторите преобразование.");
        }
        if (!result.IsValid) throw new InvalidDataException("Ошибка проверки: количество исходных ячеек не совпадает с количеством позиций.");
        result.Log.Add(result.Summary);
        result.Log.AddRange(result.Warnings.Select(w => w.ToString()));
        return result;
    }

    private static bool StartsBlock(PhysicalRow row, List<PhysicalRow> block)
    {
        if (block.Count == 0) return false;
        string name = row.Text(1), pos = row.Text(0);
        if (Regex.IsMatch(name, @"^\d+(?:\.\d+)*\s+\p{L}")) return true;
        if (pos.Length > 0 && ((row.HasQuantity && block.Any(r => r.HasQuantity)) || (name.Length > 0 && char.IsUpper(name[0])))) return true;
        // Unnumbered ordinary table records are independent; dash-prefixed variants
        // and cable cross sections keep the common block context.
        return block.Any(r => r.HasQuantity) && row.HasQuantity && name.Length > 0
            && char.IsUpper(name[0]) && !name.StartsWith('-');
    }

    private static void EmitBlock(List<PhysicalRow> block, ParseResult result)
    {
        if (block.Count == 0) return;
        var anchors = Enumerable.Range(0, block.Count).Where(i => block[i].HasQuantity).ToArray();
        if (anchors.Length == 0)
        {
            result.Warnings.Add(new(block[0].Sheet, block[0].Row, "Текстовый блок без количества не экспортирован: " + TextNormalizer.Join(block.Select(r => r.Text(1)))));
            return;
        }
        string Join(IEnumerable<PhysicalRow> rows, int col) => TextNormalizer.Join(rows.Select(r => r.Text(col)));
        if (anchors.Length == 1)
        {
            var item = Make(block[anchors[0]]);
            item.Position = string.Join(", ", block.Select(r => r.Text(0)).Where(s => s.Length > 0).Distinct());
            item.Name = Join(block, 1); item.TypeMark = Join(block, 2); item.ProductCode = Join(block, 3);
            item.Supplier = Join(block, 4); item.Unit = Join(block, 5); item.Note = Join(block, 8);
            item.UnitWeightKg = block.Select(r => r.Values[7]).FirstOrDefault(v => v is not null);
            result.Rows.Add(item);
            return;
        }
        var common = block.Take(anchors[0]).ToArray();
        string commonName = Join(common, 1), commonType = Join(common, 2), unit = Join(common, 5);
        string firstName = block[anchors[0]].Text(1);
        var crossSection = Regex.Match(firstName, @"\d+(?:[,.]\d+)?[хx×]\d+.*$");
        string firstVariantName = firstName;
        if (crossSection.Success && crossSection.Index > 0)
        {
            commonName = TextNormalizer.Join([commonName, firstName[..crossSection.Index]]);
            firstVariantName = crossSection.Value;
        }
        string position = string.Join(", ", block.Select(r => r.Text(0)).Where(s => s.Length > 0).Distinct());
        if (commonName.Length == 0) commonName = block[anchors[0]].Text(1);
        for (int n = 0; n < anchors.Length; n++)
        {
            int anchor = anchors[n], next = n + 1 < anchors.Length ? anchors[n + 1] : block.Count;
            var current = block[anchor];
            var item = Make(current);
            item.Position = position;
            var trailing = block.Skip(anchor + 1).Take(next - anchor - 1).ToArray();
            var preceding = n == 0 ? [] : block.Skip(anchors[n - 1] + 1).Take(anchor - anchors[n - 1] - 1).ToArray();
            // Name continuations follow the preceding quantity; type-only lines before
            // a quantity identify its document/design variant.
            string ownName = TextNormalizer.Join(new[] { n == 0 ? firstVariantName : current.Text(1), Join(trailing.Where(r => r.Text(1).Length > 0), 1) });
            item.Name = TextNormalizer.Join(new[] { commonName, ownName == commonName ? "" : ownName });
            string precedingType = Join(preceding.Where(r => r.Text(1).Length == 0), 2);
            if (precedingType.Length > 0) commonType = precedingType;
            item.TypeMark = TextNormalizer.Join(new[] { commonType, current.Text(2), n == anchors.Length - 1 ? Join(trailing, 2) : "" });
            item.ProductCode = TextNormalizer.Join(new[] { current.Text(3).Length > 0 ? current.Text(3) : Join(common, 3), Join(trailing, 3) });
            item.Supplier = TextNormalizer.Join(new[] { current.Text(4).Length > 0 ? current.Text(4) : Join(common, 4), Join(trailing, 4) });
            if (current.Text(5).Length > 0) unit = current.Text(5);
            item.Unit = unit;
            item.Note = TextNormalizer.Join(new[] { Join(common, 8), current.Text(8), Join(trailing, 8) });
            if (current.Text(5).Length == 0 && unit.Length > 0) Warn(result, item, "Единица измерения унаследована внутри позиции.");
            if (trailing.Length > 0 || preceding.Length > 0) Warn(result, item, "Многострочное исполнение: проверьте распределение продолжений между вариантами.");
            result.Rows.Add(item);
        }
    }
    private static SpecificationItem Make(PhysicalRow row) => new() { Quantity = row.Values[6], UnitWeightKg = row.Values[7], SourceSheet = row.Sheet, SourceRow = row.Row };
    private static void Warn(ParseResult result, SpecificationItem item, string message) => result.Warnings.Add(new(item.SourceSheet, item.SourceRow, message));
}
