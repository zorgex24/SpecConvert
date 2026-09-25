using SpecConvert.Core.Models;
using System.Text.RegularExpressions;

namespace SpecConvert.Core.Services;

public interface ISectionClassifier { bool IsSection(PhysicalRow row); }
public sealed class SectionClassifier : ISectionClassifier
{
    public bool IsSection(PhysicalRow row) => Enumerable.Range(0,9).Where(i => i != 1).All(i => row.Text(i).Length == 0)
        && Regex.IsMatch(row.Text(1), @"^((Оборудование|Материалы|Изделия и материалы)(\s*\(.*\))?|Кабели для монтажа цепей учета|Подстанция укомплектовывается:)$", RegexOptions.IgnoreCase);
}
