using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig.Content;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Scriptorium.Infrastructure.Tests.Support;

/// <summary>Builds small, valid sample files in each supported format for end-to-end tests.</summary>
internal static class SampleDocuments
{
    public static byte[] Pdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText(text, 12, new PdfPoint(25, 700), font);
        return builder.Build();
    }

    public static byte[] Docx(string text)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, autoSave: true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text(text)))));
        }

        return stream.ToArray();
    }

    public static byte[] Xlsx(string text)
    {
        using var stream = new MemoryStream();
        using (var workbook = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, autoSave: true))
        {
            var workbookPart = workbook.AddWorkbookPart();
            var sheets = new S.Sheets();
            workbookPart.Workbook = new S.Workbook(sheets);
            var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var cell = new S.Cell { DataType = S.CellValues.InlineString, InlineString = new S.InlineString(new S.Text(text)) };
            sheetPart.Worksheet = new S.Worksheet(new S.SheetData(new S.Row(cell)));
            sheets.Append(new S.Sheet { Id = workbookPart.GetIdOfPart(sheetPart), SheetId = 1, Name = "Sheet1" });
        }

        return stream.ToArray();
    }

    public static byte[] Txt(string text) => Encoding.UTF8.GetBytes(text);
}
