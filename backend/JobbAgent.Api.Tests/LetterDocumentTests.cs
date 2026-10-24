using System.Net;
using System.Net.Http.Json;
using JobbAgent.Api.Models;
using JobbAgent.Api.Services;
using UglyToad.PdfPig;

namespace JobbAgent.Api.Tests;

public class LetterDocumentTests
{
    private static readonly LetterContent Letter = new(
        "Khalid Hassan", "khalid@example.se  ·  070-123 45 67  ·  Stockholm", "Stockholm, 24 oktober 2026", "Acme AB",
        "Ansökan: Backendutvecklare", "Hej Anna!\n\nJag söker tjänsten för att jag älskar C# & .NET.\n\nVänliga hälsningar\nKhalid Hassan");

    [Fact]
    public void Docx_ContainsHeaderAndParagraphs()
    {
        var text = CvReader.ReadText(new MemoryStream(LetterDocument.Docx(Letter)), "brev.docx");
        Assert.Equal("""
            Khalid Hassan
            khalid@example.se · 070-123 45 67 · Stockholm
            Stockholm, 24 oktober 2026
            Acme AB
            Ansökan: Backendutvecklare
            Hej Anna!
            Jag söker tjänsten för att jag älskar C# & .NET.
            Vänliga hälsningar
            Khalid Hassan
            """.ReplaceLineEndings("\n"), text);
    }

    [Fact]
    public void Pdf_HasSwedishCharactersAndWrapsLongLetters()
    {
        var longLetter = Letter with { Body = string.Join("\n\n", Enumerable.Repeat("Åtta älgar övade på att skriva personliga brev i Göteborg varje dag.", 60)) };
        using var pdf = PdfDocument.Open(LetterDocument.Pdf(longLetter));
        Assert.True(pdf.NumberOfPages >= 2);
        var first = pdf.GetPage(1).Text;
        Assert.Contains("Khalid Hassan", first);
        Assert.Contains("Åtta älgar övade", first);

        using var shortPdf = PdfDocument.Open(LetterDocument.Pdf(Letter));
        Assert.Equal(1, shortPdf.NumberOfPages);
        Assert.Contains("C# & .NET.", shortPdf.GetPage(1).Text);
    }

    [Fact]
    public void Wrap_KeepsLinesWithinWidth()
    {
        var lines = LetterDocument.Wrap(string.Join(" ", Enumerable.Repeat("ord", 100)), s => s.Length * 10.0).ToList();
        Assert.True(lines.Count > 1);
        Assert.All(lines, l => Assert.True(l.Length * 10.0 <= 455));
    }

    [Fact]
    public void Content_UsesProfileAndLanguage()
    {
        var run = new Run { Title = "Developer", Company = "Acme: Nordic", Language = "en", Letter = "Dear team" };
        var profile = new Profile { FullName = "Khalid Hassan", Email = "k@example.se", City = "Malmö" };
        var content = LetterContent.From(run, profile, new DateTime(2026, 10, 24));

        Assert.Equal(("Application: Developer", "Malmö, 24 October 2026", "k@example.se  ·  Malmö"), (content.Subject, content.DateLine, content.ContactLine));
        Assert.Equal("Cover letter - Acme Nordic.pdf", content.FileName("pdf"));
    }

    [Fact]
    public void ContactInfo_IsGuessedFromCv()
    {
        var contact = ContactInfo.Guess("Khalid Hassan\nSystemutvecklare\nE-post: khalid.hassan@example.se · Tel: 070-123 45 67");
        Assert.Equal(("Khalid Hassan", "khalid.hassan@example.se", "070-123 45 67"), (contact.FullName, contact.Email, contact.Phone));
        Assert.Null(ContactInfo.Guess("CURRICULUM VITAE 2026\nfoo").FullName);
    }

    [Fact]
    public async Task Download_ReturnsWordAndPdf()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await TestApp.SaveCvAsync(client, "Khalid Hassan\n" + OrchestratorTests.Cv + " khalid@example.se");
        var letter = await app.RunLetterAsync(client);

        var pdf = await client.GetAsync($"/api/runs/{letter.Id}/letter.pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        using (var doc = PdfDocument.Open(await pdf.Content.ReadAsByteArrayAsync()))
        {
            var text = doc.GetPage(1).Text;
            Assert.Contains("Khalid Hassan", text);
            Assert.Contains("khalid@example.se", text);
            Assert.Contains("Hej Anna!", text);
        }

        var docx = await client.GetAsync($"/api/runs/{letter.Id}/letter.docx");
        Assert.Equal(HttpStatusCode.OK, docx.StatusCode);
        Assert.Equal("Personligt brev - Acme AB.docx", docx.Content.Headers.ContentDisposition!.FileNameStar);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/runs/{letter.Id}/letter.exe")).StatusCode);
    }
}
