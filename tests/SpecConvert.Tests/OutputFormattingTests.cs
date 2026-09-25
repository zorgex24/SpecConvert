using ClosedXML.Excel;
using SpecConvert.Core.Models;
using SpecConvert.Core.Services;
using Xunit;

namespace SpecConvert.Tests;

public sealed class OutputFormattingTests
{
    [Theory]
    [InlineData("1.10 Антенна 3G/GSM", "Антенна 3G/GSM")]
    [InlineData(" 12) Кабель КПСнг(А)-FRHF 1х2х0,5", "Кабель КПСнг(А)-FRHF 1х2х0,5")]
    [InlineData("№ 3 — Стойка МШК1-10-100", "Стойка МШК1-10-100")]
    [InlineData("- контактной сети 10 кВ", "контактной сети 10 кВ")]
    [InlineData("42Шкаф", "Шкаф")]
    [InlineData("Кабель 2х2х0,52", "Кабель 2х2х0,52")]
    [InlineData("123...", "")]
    [InlineData("", "")]
    public void NamesStartAtFirstLetterWithoutChangingInternalNumbers(string input, string expected)
        => Assert.Equal(expected, TextNormalizer.Name(input));

    [Fact]
    public void NumberingSkipsSectionsAndMissingQuantitiesButIncludesZeroAndText()
    {
        SpecificationItem[] rows = [
            new() { Position="old", Name="1 Раздел", RowType=SpecificationRowType.SectionHeader },
            new() { Position="РП", Name="2 Изделие", Quantity=0d },
            new() { Position="old", Name="Без количества" },
            new() { Position="old", Name="Пробел", Quantity=" " },
            new() { Name="Материалы", RowType=SpecificationRowType.SectionHeader },
            new() { Position="РП", Name="3 Изделие", Quantity="по проекту" }
        ];
        OutputRowFormatter.Apply(rows);
        Assert.Equal(new[] { "", "1", "", "", "", "2" }, rows.Select(r=>r.Position));
        Assert.Equal("Изделие", rows[1].Name);
        OutputRowFormatter.Apply(rows);
        Assert.Equal("2", rows[5].Position);
    }

    [Fact]
    public void ExportReappliesRulesAfterManualEditsWithoutMutatingPreview()
    {
        string file=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");
        var edited=new SpecificationItem { Position="99", Name="42. Кабель 1х2х0,5", Quantity=2d };
        try
        {
            new ExcelExporter().Export(file,[edited,new(){Name="Раздел",RowType=SpecificationRowType.SectionHeader},new(){Position="7",Name="Пустая",Quantity=null},new(){Position="9",Name="5. Шкаф",Quantity=0d}]);
            using var book=new XLWorkbook(file); var sheet=book.Worksheet(1);
            Assert.Equal("1",sheet.Cell(3,1).GetString()); Assert.Equal("Кабель 1х2х0,5",sheet.Cell(3,2).GetString());
            Assert.True(sheet.Cell(4,1).IsEmpty()); Assert.True(sheet.Cell(5,1).IsEmpty());
            Assert.Equal("2",sheet.Cell(6,1).GetString()); Assert.Equal("Шкаф",sheet.Cell(6,2).GetString());
            Assert.Equal("99",edited.Position); Assert.Equal("42. Кабель 1х2х0,5",edited.Name);
        }
        finally { File.Delete(file); }
    }
}
