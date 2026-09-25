using ClosedXML.Excel;
using SpecConvert.Core.Models;
using SpecConvert.Core.Services;
using Xunit;

namespace SpecConvert.Tests;
public sealed class ExcelTests
{
    [Theory]
    [InlineData("13", "13")]
    [InlineData("0", "0")]
    [InlineData("-2", "-2")]
    [InlineData("13,0", "13")]
    [InlineData("12,5", "12,5")]
    [InlineData("0,125", "0,125")]
    public void ExportDisplaysNumbersWithoutTrailingDecimalSeparator(string value, string expected)
    {
        string file = Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");
        try
        {
            new ExcelExporter().Export(file,[new() { Name="Изделие", Quantity=value, UnitWeightKg=value }]);
            using var book = new XLWorkbook(file);
            foreach (int column in new[] { 7, 8 })
            {
                var cell = book.Worksheet(1).Cell(3,column);
                Assert.Equal(XLDataType.Number,cell.DataType);
                Assert.Equal(expected,cell.GetFormattedString(System.Globalization.CultureInfo.GetCultureInfo("ru-RU")));
            }
        }
        finally { File.Delete(file); }
    }

    [Fact] public void ExportKeepsEditsNumbersBlanksAndSectionOrder()
    {
        string file = Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");
        try
        {
            new ExcelExporter().Export(file,[new() { RowType=SpecificationRowType.SectionHeader,Name="Материалы" }, new() { Position="Т1, Т2",Name="Исправленное имя",Quantity="12,5",UnitWeightKg=null,Note="=НЕ ФОРМУЛА" }]);
            using var book = new XLWorkbook(file); var s = book.Worksheet(1);
            for (int col = 1; col <= 9; col++)
            {
                Assert.Equal(ExcelExporter.Headers[col-1],s.Cell(1,col).GetString());
                Assert.Equal(XLDataType.Number,s.Cell(2,col).DataType);
                Assert.Equal(col,s.Cell(2,col).GetDouble());
            }
            Assert.Equal(2,s.SheetView.SplitRow);
            Assert.Equal(2,s.AutoFilter.Range.RangeAddress.FirstAddress.RowNumber);
            Assert.Equal("Материалы",s.Cell(3,2).GetString()); Assert.Equal("Исправленное имя",s.Cell(4,2).GetString());
            Assert.Equal(12.5,s.Cell(4,7).GetDouble()); Assert.True(s.Cell(4,8).IsEmpty()); Assert.False(s.Cell(4,9).HasFormula);
            Assert.Empty(s.MergedRanges); Assert.Equal(9,s.LastColumnUsed()!.ColumnNumber());
        }
        finally { File.Delete(file); }
    }
    [Fact] public void ReaderUsesCachedFormulaAndMergedOrigin()
    {
        string file = Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");
        try
        {
            using (var book = new XLWorkbook())
            {
                var s = book.AddWorksheet("Формулы");
                for(int i=0;i<9;i++) s.Cell(1,i+1).Value=ExcelExporter.Headers[i];
                s.Cell(2,2).Value="Изделие"; s.Cell(2,7).FormulaA1="2+3"; s.Range("G2:G4").Merge();
                book.SaveAs(file,new SaveOptions { EvaluateFormulasBeforeSaving=true });
            }
            var sheets = new ExcelReader().Read(file);
            Assert.Equal(5d,new CellValueResolver(sheets[0]).Resolve(4,7)!.Value);
            var result = new SpecificationParser().Parse(sheets); Assert.Equal(5d,Assert.Single(result.Rows).Quantity);
        }
        finally { File.Delete(file); }
    }
    [Fact] public void FailedExportDoesNotReplaceExistingDestination()
    {
        string file = Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");
        try
        {
            File.WriteAllText(file,"original");
            using(var locked = File.Open(file,FileMode.Open,FileAccess.Read,FileShare.None))
            {
                var error = Record.Exception(()=>new ExcelExporter().Export(file,[new(){Name="test",Quantity=1d}]));
                Assert.True(error is IOException or UnauthorizedAccessException);
            }
            Assert.Equal("original",File.ReadAllText(file));
        }
        finally { File.Delete(file); }
    }
    [Fact] public void CorruptedWorkbookRaisesControlledServiceError()
    {
        string file=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");
        try { File.WriteAllText(file,"not a workbook"); Assert.ThrowsAny<Exception>(()=>new ExcelReader().Read(file)); }
        finally { File.Delete(file); }
    }
}
