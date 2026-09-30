using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using Scriptorium.Infrastructure.Parsing;

namespace Scriptorium.Infrastructure.Tests.Parsing;

public class ExcelDocumentParserTests
{
    private readonly ExcelDocumentParser _parser = new();

    [Fact]
    public void SupportedFileType_IsXlsx()
    {
        _parser.SupportedFileType.Should().Be("xlsx");
    }

    [Fact]
    public async Task ExtractTextAsync_MultiSheetWorkbook_ReturnsTextFromAllSheets()
    {
        using var stream = BuildWorkbook(["Alpha", "Beta"], ["Gamma"]);

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Alpha").And.Contain("Beta").And.Contain("Gamma");
    }

    [Fact]
    public async Task ExtractTextAsync_NumericCell_ReturnsRawValue()
    {
        using var stream = BuildWorkbook(["Total"], numericSheet: 42);

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("42");
    }

    [Fact]
    public async Task ExtractTextAsync_CorruptedXlsx_ReturnsFailure()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not a zip package"));

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Corrupted");
    }

    private static MemoryStream BuildWorkbook(string[] sheet1, string[]? sheet2 = null, int? numericSheet = null)
    {
        var stream = new MemoryStream();
        using (var workbook = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, autoSave: true))
        {
            var workbookPart = workbook.AddWorkbookPart();
            var sheets = new Sheets();
            workbookPart.Workbook = new Workbook(sheets);
            AddSheet(workbookPart, sheets, 1, sheet1, numericSheet);
            if (sheet2 is not null)
            {
                AddSheet(workbookPart, sheets, 2, sheet2, null);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static void AddSheet(WorkbookPart workbookPart, Sheets sheets, uint id, string[] values, int? number)
    {
        var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var row = new Row(values.Select(v => new Cell
        {
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(v))
        }));
        if (number is not null)
        {
            row.Append(new Cell { CellValue = new CellValue(number.Value) });
        }

        sheetPart.Worksheet = new Worksheet(new SheetData(row));
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(sheetPart),
            SheetId = id,
            Name = $"Sheet{id}"
        });
    }
}
