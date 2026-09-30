using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Infrastructure.Parsing;

public sealed class ExcelDocumentParser : IDocumentParser
{
    public string SupportedFileType => "xlsx";

    public Task<Result<string>> ExtractTextAsync(Stream content, CancellationToken ct)
    {
        return Task.FromResult(Extract(content, ct));
    }

    private static Result<string> Extract(Stream content, CancellationToken ct)
    {
        try
        {
            using var workbook = SpreadsheetDocument.Open(content, false);
            var workbookPart = workbook.WorkbookPart;
            if (workbookPart is null)
            {
                return Result<string>.Failure("Corrupted or unreadable Excel workbook");
            }

            var sheets = workbookPart.WorksheetParts.Select(sheet => ReadSheet(sheet, workbookPart, ct));
            return Result<string>.Success(string.Join(Environment.NewLine, sheets).Trim());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<string>.Failure("Corrupted or unreadable Excel workbook");
        }
    }

    private static string ReadSheet(WorksheetPart sheet, WorkbookPart workbookPart, CancellationToken ct)
    {
        var rows = (sheet.Worksheet?.Descendants<Row>() ?? Enumerable.Empty<Row>()).Select(row =>
        {
            ct.ThrowIfCancellationRequested();
            var cells = row.Elements<Cell>()
                .Select(cell => ReadCell(cell, workbookPart))
                .Where(text => text.Length > 0);
            return string.Join('\t', cells);
        });

        return string.Join(Environment.NewLine, rows.Where(r => r.Length > 0));
    }

    private static string ReadCell(Cell cell, WorkbookPart workbookPart)
    {
        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText ?? string.Empty;
        }

        var raw = cell.CellValue?.Text ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(raw, out var index))
        {
            var item = workbookPart.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().ElementAtOrDefault(index);
            return item?.InnerText ?? string.Empty;
        }

        return raw;
    }
}
