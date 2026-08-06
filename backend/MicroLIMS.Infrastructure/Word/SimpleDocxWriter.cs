using System.IO.Compression;
using System.Text;

namespace MicroLIMS.Infrastructure.Word;

// A dependency-free minimal .docx writer, same philosophy as
// SimplePdfWriter: a genuinely valid OOXML Word document (opens in
// Word/LibreOffice/Google Docs) built from a title + a flat list of
// text lines, not a full-fidelity layout engine. No pagination limit
// like the PDF writer - Word paginates the flowed text itself.
public static class SimpleDocxWriter
{
    public static byte[] WriteTextDocument(string title, IEnumerable<string> lines)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(archive, "_rels/.rels", RelsXml);
            WriteEntry(archive, "word/document.xml", BuildDocumentXml(title, lines));
        }
        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string BuildDocumentXml(string title, IEnumerable<string> lines)
    {
        var body = new StringBuilder();
        body.Append("<w:p><w:pPr><w:jc w:val=\"center\"/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val=\"32\"/></w:rPr><w:t xml:space=\"preserve\">")
            .Append(Escape(title))
            .Append("</w:t></w:r></w:p>");
        body.Append("<w:p/>");

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                body.Append("<w:p/>");
                continue;
            }

            // A line ending with ':' followed by nothing, or a bare
            // section heading in Title Case with no leading indent, reads
            // as a heading - bold it. Everything else is a plain paragraph.
            var isHeading = !line.StartsWith(' ') && !line.StartsWith('-') && line.EndsWith(':') == false && line.Length < 60 && char.IsUpper(line.TrimStart().FirstOrDefault());
            var runProps = isHeading ? "<w:rPr><w:b/></w:rPr>" : "";
            body.Append("<w:p><w:r>").Append(runProps).Append("<w:t xml:space=\"preserve\">")
                .Append(Escape(line))
                .Append("</w:t></w:r></w:p>");
        }

        return $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:xml="http://www.w3.org/XML/1998/namespace">
          <w:body>
            {body}
            <w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440"/></w:sectPr>
          </w:body>
        </w:document>
        """;
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private const string ContentTypesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
        </Types>
        """;

    private const string RelsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
        </Relationships>
        """;
}
