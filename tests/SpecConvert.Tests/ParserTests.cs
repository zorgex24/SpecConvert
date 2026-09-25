using ClosedXML.Excel;
using SpecConvert.Core.Models;
using SpecConvert.Core.Services;
using Xunit;

namespace SpecConvert.Tests;

public sealed class ParserTests
{
    private static SourceSheet Sheet(string name = "Лист", int offset = 0)
    {
        var s = new SourceSheet(name);
        for (int i = 0; i < 9; i++) Set(s, 1, i + 1 + offset, ExcelExporter.Headers[i]);
        return s;
    }
    private static void Set(SourceSheet s, int r, int c, object value) => s.Cells[(r,c)] = new(r,c,value);
    private static void Row(SourceSheet s, int row, string pos = "", string name = "", string type = "", string unit = "", object? qty = null, object? mass = null)
    {
        object?[] values = [pos,name,type,null,null,unit,qty,mass,null];
        for (int c = 0; c < 9; c++) if (values[c] is not null && values[c]!.ToString() != "") Set(s,row,c+1,values[c]!);
    }
    [Fact] public void NormalizationPreservesDesignations()
    {
        Assert.Equal("КПСнг(А)-FRHF 1х2х0,5", TextNormalizer.Normalize(" КПСнг(А)-FRHF\t1х2х0,5_x000D_\n"));
        Assert.Equal("едизмерения", TextNormalizer.Header("Ед.\nизме-\nре-\nния"));
    }
    [Fact] public void HeadersAreNotFixedCoordinates()
    {
        var s = Sheet(offset: 12); var l = Assert.Single(new SourceLayoutDetector().Detect(s));
        Assert.Equal(19,l.Columns[6]!.First);
    }
    [Fact] public void MultilineFieldsAndMultiplePositionLabels()
    {
        var s = Sheet(); Row(s,2,"ШУ1","Шкаф", "Тип"); Row(s,3,"ШУ3","с приборами", "ГОСТ", "шт.",2d); Row(s,4,name:"и кабелями");
        var i = Assert.Single(new SpecificationParser().Parse([s]).Rows);
        Assert.Equal("1",i.Position); Assert.Equal("Шкаф с приборами и кабелями",i.Name); Assert.Equal("Тип ГОСТ",i.TypeMark);
    }
    [Fact] public void VariantsInheritOnlyWithinBlock()
    {
        var s = Sheet(); Row(s,2,"3","Стойка"); Row(s,3,type:"МШК1",unit:"шт.",qty:13d); Row(s,4,type:"МШК2",qty:88d);
        Row(s,5,"4","Другая"); Row(s,6,type:"Б",qty:1d);
        var r = new SpecificationParser().Parse([s]);
        Assert.Equal(3,r.ItemCount); Assert.Equal("2",r.Rows[1].Position); Assert.Equal("Стойка",r.Rows[1].Name); Assert.Equal("шт.",r.Rows[1].Unit); Assert.Equal("",r.Rows[2].Unit);
    }
    [Fact] public void SectionIsNotContinuation()
    {
        var s = Sheet(); Row(s,2,name:"Материалы"); Row(s,3,"1","Кабель",qty:2d); Row(s,4,name:"огнестойкий");
        var r = new SpecificationParser().Parse([s]); Assert.Equal(1,r.SectionCount); Assert.Equal("Кабель огнестойкий",r.Rows[1].Name);
    }
    [Fact] public void SideNotesDoNotStopButStampDoes()
    {
        var s = Sheet(offset: 2); Set(s,2,4,"Изделие"); Set(s,2,9,1d); Set(s,3,1,"Подпись и дата"); Set(s,4,4,"Другое"); Set(s,4,9,2d);
        Set(s,5,3,"Изм."); Set(s,5,4,"Кол.уч."); Set(s,6,9,999d);
        Assert.Equal(2,new SpecificationParser().Parse([s]).ItemCount);
    }
    [Fact] public void ContextCrossesSheets()
    {
        var a = Sheet("A"); var b = Sheet("B"); Row(a,2,"12","Фиксатор"); Row(a,3,type:"ФП",unit:"шт.",qty:1d); Row(b,2,type:"ФО",qty:2d);
        var r = new SpecificationParser().Parse([a,b]); Assert.Equal("Фиксатор",r.Rows[1].Name); Assert.Equal("2",r.Rows[1].Position); Assert.Equal("B",r.Rows[1].SourceSheet);
    }
    [Fact] public void MergedQuantityCountedOnceAndNumbersPreserved()
    {
        var s = Sheet(); Row(s,2,name:"Изделие",qty:12.5d,mass:.12d); s.Merges.Add(new(2,7,4,7));
        Row(s,3,name:"продолжение"); Row(s,5,"2","Второе",qty:0d);
        var r = new SpecificationParser().Parse([s]); Assert.Equal(2,r.QuantityAnchors); Assert.Equal(12.5d,r.Rows[0].Quantity); Assert.Equal(.12d,r.Rows[0].UnitWeightKg); Assert.Null(r.Rows[1].UnitWeightKg); Assert.Equal("",r.Rows[1].Supplier);
    }
    [Fact] public void TextQuantityIsKeptWithWarning()
    {
        var s = Sheet(); Row(s,2,name:"Изделие",qty:"по проекту"); var r = new SpecificationParser().Parse([s]);
        Assert.Single(r.Rows); Assert.Equal("по проекту",r.Rows[0].Quantity); Assert.Contains(r.Warnings,w=>w.Message.Contains("как текст"));
    }
    [Fact] public void EmptyAndUnknownSheetsWarn()
    { Assert.Single(new SpecificationParser().Parse([new SourceSheet("Empty")]).Warnings); }
    [Fact] public void CommonNameAndDashVariants()
    {
        var s = Sheet(); Row(s,2,"4","Комплект установки ОПН", "КС.ОПН"); Row(s,3,name:"- контактной сети",type:"ОПН-3",unit:"шт.",qty:9d); Row(s,4,name:"на металлической стойке"); Row(s,5,name:"- ВЛ 10 кВ",type:"ОПН-10",qty:5d);
        var r = new SpecificationParser().Parse([s]); Assert.Equal(2,r.ItemCount); Assert.Contains("на металлической стойке",r.Rows[0].Name); Assert.Contains("Комплект установки ОПН - ВЛ",r.Rows[1].Name); Assert.DoesNotContain("контактной сети",r.Rows[1].Name);
    }
    [Theory] [InlineData("Исходник 1.xlsx",6)] [InlineData("Исходник 2.xlsx",7)]
    public void SourceIntegration(string filename, int sheets)
    {
        string root = FindRoot(); var source = new ExcelReader().Read(Path.Combine(root,"docs",filename)); var result = new SpecificationParser().Parse(source);
        // Independent control for the inspected fixtures: BO below the header, above the drawing stamp.
        int expected = source.Sum(s => s.Cells.Values.Count(c=> c.Column == 67 && c.Row > 8 && c.Row < 104 && c.Value is not null && !string.IsNullOrWhiteSpace(c.Value.ToString())));
        Assert.Equal(sheets,result.SheetsProcessed); Assert.Equal(expected,result.ItemCount); Assert.Equal(expected,result.QuantityAnchors);
        Assert.Equal(Enumerable.Range(1,expected).Select(n=>n.ToString()),result.Rows.Where(r=>r.RowType==SpecificationRowType.Item).Select(r=>r.Position));
        Assert.All(result.Rows,r=>Assert.True(r.Name.Length>0 && char.IsLetter(r.Name[0])));
        Assert.All(result.Rows.Where(r=>r.RowType==SpecificationRowType.SectionHeader),r=>Assert.Empty(r.Position));
        Assert.DoesNotContain(result.Rows,r=>r.Name.Contains("Разраб.") || r.TypeMark.Contains("Кол.уч."));
        if (filename.Contains('1'))
        {
            Assert.Contains("индивидуальной разработки",result.Rows.First(r=>r.SourceSheet=="Лист1" && r.SourceRow==18).Name);
            Assert.Contains(result.Rows,r=>r.SourceSheet=="Лист1" && r.SourceRow==57 && r.Name.Contains("коробками испытательными"));
            Assert.DoesNotContain(result.Rows.Where(r=>r.RowType==SpecificationRowType.Item),r=>r.Name.Contains("Подстанция укомплектовывается") || r.Name.Contains("Кабели для монтажа"));
            var cableVariants=result.Rows.Where(r=>r.SourceSheet=="Лист6" && r.SourceRow is 60 or 63).ToList();
            Assert.Equal(2,cableVariants.Count);
            Assert.All(cableVariants,r=>Assert.Contains("газовыделением:",r.Name));
        }
        else
        {
            var stands = result.Rows.Where(r=>r.Name.Contains("Стойка металлическая")).ToList(); Assert.Equal(7,stands.Count);
            Assert.Contains(stands,r=>r.TypeMark.Contains("МШП1-10-120") && r.Unit=="шт." && r.UnitWeightKg is null);
            Assert.Contains(result.Rows,r=>r.SourceSheet=="Лист4" && r.SourceRow==9 && r.Name.Contains("Фиксатор"));
        }
        string dir = Path.Combine(root,"outputs"); Directory.CreateDirectory(dir);
        string output = Path.Combine(dir,Path.GetFileNameWithoutExtension(filename)+" — результат.xlsx");
        new ExcelExporter().Export(output,result.Rows);
        using var exported = new XLWorkbook(output);
        Assert.Equal(result.Rows.Count+2,exported.Worksheet(1).LastRowUsed()!.RowNumber());
        Assert.Equal(XLDataType.Number,exported.Worksheet(1).CellsUsed().First(c=>c.Address.ColumnNumber==7 && c.Address.RowNumber>2).DataType);
        File.WriteAllLines(Path.ChangeExtension(output,".log"),result.Log);
    }
    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName,"docs"))) dir=dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Не найдены исходные файлы docs.");
    }
}
