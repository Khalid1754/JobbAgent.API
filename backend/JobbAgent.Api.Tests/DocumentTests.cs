using System.IO.Compression;
using System.Net;
using System.Text;
using JobbAgent.Api.Agents;
using JobbAgent.Api.Services;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace JobbAgent.Api.Tests;

public class DocumentTests
{
    private const string Long = "Systemutvecklare med erfarenhet av C#, .NET, React och SQL Server sedan flera år.";

    [Fact]
    public void Docx_ParagraphsBecomeLines()
    {
        using var file = new MemoryStream();
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
        {
            writer.Write($"""
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>
                <w:p><w:r><w:t>Khalid</w:t></w:r><w:r><w:t xml:space="preserve"> Hassan</w:t></w:r></w:p>
                <w:p><w:r><w:t>{Long}</w:t></w:r></w:p>
                </w:body></w:document>
                """);
        }
        file.Position = 0;

        Assert.Equal($"Khalid Hassan\n{Long}", CvReader.ReadText(file, "cv.docx"));
    }

    [Fact]
    public void Pdf_TextIsExtracted()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText("Khalid Hassan", 14, new PdfPoint(40, 780), font);
        // Standardtypsnitten i testets PDF klarar bara enkla tecken.
        page.AddText("Systemutvecklare med erfarenhet av dotnet, React och SQL Server sedan flera ar.", 10, new PdfPoint(40, 750), font);

        var text = CvReader.ReadText(new MemoryStream(builder.Build()), "CV.PDF");
        Assert.Contains("Khalid Hassan", text);
        Assert.Contains("SQL Server sedan flera ar", text);
    }

    [Fact]
    public void TextFile_IsTidied()
    {
        var text = CvReader.ReadText(new MemoryStream(Encoding.UTF8.GetBytes($"  Khalid   Hassan \r\n\r\n\r\n\r\n{Long}")), "cv.txt");
        Assert.Equal($"Khalid Hassan\n\n{Long}", text);
    }

    [Theory]
    [InlineData("cv.png", "Ladda upp CV:t som PDF")]
    [InlineData("cv.txt", "nästan ingen text")]
    [InlineData("cv.pdf", "kunde inte läsas")]
    public void BadFiles_GiveSwedishErrors(string name, string expected)
    {
        var error = Assert.Throws<DocumentException>(() => CvReader.ReadText(new MemoryStream("kort"u8.ToArray()), name));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void Html_BecomesReadableText()
    {
        var text = AdReader.HtmlToText("""
            <html><head><style>p{color:red}</style><script>alert(1)</script></head>
            <body><nav>Meny</nav><h1>Backendutvecklare</h1><p>Vi s&ouml;ker dig som kan <b>C#</b>.</p><ul><li>API</li><li>SQL</li></ul>
            <footer>Cookies</footer></body></html>
            """);
        Assert.Equal("Backendutvecklare\nVi söker dig som kan C# .\n\nAPI\n\nSQL", text);
    }

    [Theory]
    [InlineData("https://arbetsformedlingen.se/platsbanken/annonser/29812345", "29812345")]
    [InlineData("https://arbetsformedlingen.se/platsbanken/annonser/29812345?ref=x", "29812345")]
    [InlineData("https://www.linkedin.com/jobs/view/123", null)]
    public void PlatsbankenLinks_AreRecognised(string url, string? id) => Assert.Equal(id, AdReader.PlatsbankenId(url));

    [Fact]
    public async Task AdReader_FetchesPage()
    {
        var html = $"<html><body><main><h1>Backendutvecklare</h1><p>{string.Join(" ", Enumerable.Repeat(Long, 4))}</p></main></body></html>";
        var reader = new AdReader(new HttpClient(new FakeHandler(HttpStatusCode.OK, html, "text/html")));
        var text = await reader.ReadAsync("https://example.org/jobb/1", CancellationToken.None);
        Assert.StartsWith("Backendutvecklare\n", text);
    }

    [Theory]
    [InlineData("ftp://example.org/jobb", "http")]
    [InlineData("inte en länk", "http")]
    public async Task AdReader_RejectsBadLinks(string url, string expected)
    {
        var reader = new AdReader(new HttpClient(new FakeHandler(HttpStatusCode.OK, "")));
        var error = await Assert.ThrowsAsync<DocumentException>(() => reader.ReadAsync(url, CancellationToken.None));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public async Task AdReader_LoginPage_AsksForText()
    {
        var reader = new AdReader(new HttpClient(new FakeHandler(HttpStatusCode.OK, "<p>Logga in</p>", "text/html")));
        var error = await Assert.ThrowsAsync<DocumentException>(() => reader.ReadAsync("https://example.org", CancellationToken.None));
        Assert.Contains("Klistra in annonstexten", error.Message);
    }

    [Fact]
    public void Interleave_MixesQueriesAndRemovesDuplicates()
    {
        var a = new[] { FakeJobBoard.Ad("1", "A"), FakeJobBoard.Ad("2", "B"), FakeJobBoard.Ad("3", "C") };
        var b = new[] { FakeJobBoard.Ad("2", "B"), FakeJobBoard.Ad("4", "D") };
        Assert.Equal(["1", "2", "4", "3"], Searcher.Interleave([a, b], 10).Select(x => x.Id));
        Assert.Equal(["1", "2"], Searcher.Interleave([a, b], 2).Select(x => x.Id));
    }
}
