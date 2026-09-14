using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NaturesSwiftnessParse
{
    // A formula cell -- e.g. a HYPERLINK() -- with a cached display value written alongside it so it
    // renders correctly even before the host spreadsheet app recalculates. Formula text is given
    // without the leading "=".
    public class XlsxFormula
    {
        public string Formula { get; }
        public string CachedValue { get; }

        public XlsxFormula(string formula, string cachedValue)
        {
            Formula = formula;
            CachedValue = cachedValue;
        }
    }

    // Minimal, dependency-free .xlsx (OOXML) writer -- just enough to produce a multi-sheet workbook
    // of plain string/number cells plus the odd hyperlink formula cell, which is all the Windfury
    // report needs (see WindfuryUptimeParse.WriteXlsxSummary). Not a general-purpose spreadsheet
    // library: no cell styling beyond Excel's/Sheets' built-in default, no shared-string table
    // (cells use inline strings instead -- these sheets are far too small for that to matter), no
    // formula recalculation engine (relies on the cached value + the host app recalculating on open).
    // Built by hand over System.IO.Compression.ZipArchive rather than pulling in a charting/OOXML
    // NuGet package (ClosedXML/EPPlus etc.) -- this project manages its packages by hand-wired
    // hintpaths rather than a working NuGet restore, so a heavy new dependency graph would be more
    // fragile here than ~150 lines of fixed-shape XML.
    public class XlsxWriter
    {
        private readonly List<(string Name, List<object[]> Rows)> _sheets = new List<(string, List<object[]>)>();

        // rows: each entry is one spreadsheet row, left-to-right by array index (column A, B, C...).
        // Cell values may be null/"" (left blank), string, a numeric type (double/int/long/float,
        // written as a number cell), or XlsxFormula.
        public void AddSheet(string name, List<object[]> rows)
        {
            _sheets.Add((name, rows));
        }

        public void Save(string path)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
                WriteEntry(archive, "_rels/.rels", BuildRootRelsXml());
                WriteEntry(archive, "xl/workbook.xml", BuildWorkbookXml());
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
                WriteEntry(archive, "xl/styles.xml", BuildStylesXml());

                for (int i = 0; i < _sheets.Count; i++)
                {
                    WriteEntry(archive, $"xl/worksheets/sheet{i + 1}.xml", BuildSheetXml(_sheets[i].Rows));
                }
            }
        }

        private static void WriteEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        private string BuildContentTypesXml()
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (int i = 0; i < _sheets.Count; i++)
            {
                sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }
            sb.Append("</Types>");
            return sb.ToString();
        }

        private static string BuildRootRelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>";
        }

        private string BuildWorkbookXml()
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            sb.Append("<sheets>");
            for (int i = 0; i < _sheets.Count; i++)
            {
                sb.Append($"<sheet name=\"{XmlEscape(_sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
            }
            sb.Append("</sheets>");
            sb.Append("</workbook>");
            return sb.ToString();
        }

        private string BuildWorkbookRelsXml()
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 0; i < _sheets.Count; i++)
            {
                sb.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
            }
            // Styles gets the next id after every sheet's.
            sb.Append($"<Relationship Id=\"rId{_sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        private static string BuildStylesXml()
        {
            // Bare-minimum valid styles part -- one default font/fill/border/cellXf (index 0), which
            // Excel/Sheets require to be present even when nothing is actually styled.
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
                "<borders count=\"1\"><border/></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
                "</styleSheet>";
        }

        private static string BuildSheetXml(List<object[]> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<sheetData>");

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                sb.Append($"<row r=\"{rowIndex + 1}\">");
                for (int colIndex = 0; colIndex < row.Length; colIndex++)
                {
                    AppendCell(sb, rowIndex, colIndex, row[colIndex]);
                }
                sb.Append("</row>");
            }

            sb.Append("</sheetData>");
            sb.Append("</worksheet>");
            return sb.ToString();
        }

        private static void AppendCell(StringBuilder sb, int rowIndex, int colIndex, object value)
        {
            if (value == null || (value is string emptyCheck && emptyCheck.Length == 0)) return; // leave blank cells out entirely

            string cellRef = ColumnName(colIndex) + (rowIndex + 1);

            if (value is XlsxFormula formula)
            {
                sb.Append($"<c r=\"{cellRef}\" t=\"str\">");
                sb.Append($"<f>{XmlEscape(formula.Formula)}</f>");
                sb.Append($"<v>{XmlEscape(formula.CachedValue)}</v>");
                sb.Append("</c>");
            }
            else if (value is string str)
            {
                sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{XmlEscape(str)}</t></is></c>");
            }
            else if (value is double || value is int || value is long || value is float)
            {
                string numStr = Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                sb.Append($"<c r=\"{cellRef}\"><v>{numStr}</v></c>"); // no t attribute -- "n" (number) is the default cell type
            }
            else
            {
                sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{XmlEscape(value.ToString())}</t></is></c>");
            }
        }

        // 0 -> A, 25 -> Z, 26 -> AA, ... (spreadsheet column naming is base-26 with no zero digit).
        private static string ColumnName(int colIndex)
        {
            int n = colIndex + 1;
            var sb = new StringBuilder();
            while (n > 0)
            {
                int rem = (n - 1) % 26;
                sb.Insert(0, (char)('A' + rem));
                n = (n - 1) / 26;
            }
            return sb.ToString();
        }

        private static string XmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }
    }
}
