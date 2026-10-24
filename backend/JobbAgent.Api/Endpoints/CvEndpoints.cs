using JobbAgent.Api.Data;
using JobbAgent.Api.Models;
using JobbAgent.Api.Services;
using Microsoft.EntityFrameworkCore;
using static JobbAgent.Api.Services.Language;

namespace JobbAgent.Api.Endpoints;

// Dina CV-versioner. Agenterna använder standard-CV:t om du inte väljer ett annat.
public static class CvEndpoints
{
    public const int MinCvLength = 50;
    public const int MaxCvLength = 30000;
    public const long MaxFileSize = 5 * 1024 * 1024;
    public const int MaxCvs = 10;

    public static void MapCvEndpoints(this IEndpointRouteBuilder app)
    {
        var cvs = app.MapGroup("/api/cvs");

        cvs.MapGet("/", async (JobbAgentDbContext db, CancellationToken ct) =>
            (await db.Profiles.AsNoTracking().OrderByDescending(p => p.IsDefault).ThenBy(p => p.Id).ToListAsync(ct)).Select(CvDto.From));

        cvs.MapPost("/", async (SaveCvRequest request, JobbAgentDbContext db, CancellationToken ct) =>
        {
            if (await db.Profiles.CountAsync(ct) >= MaxCvs)
                return Results.BadRequest(new { error = L($"Du kan ha högst {MaxCvs} CV.", $"You can have at most {MaxCvs} CVs.") });
            var cv = new Profile
            {
                Id = (await db.Profiles.MaxAsync(p => (int?)p.Id, ct) ?? 0) + 1,
                IsDefault = !await db.Profiles.AnyAsync(ct),
            };
            return await SaveAsync(db, cv, Text.Tidy(request.CvText ?? ""), null, request, isNew: true, ct);
        });

        cvs.MapPut("/{id:int}", async (int id, SaveCvRequest request, JobbAgentDbContext db, CancellationToken ct) =>
            await db.Profiles.FindAsync([id], ct) is { } cv
                ? await SaveAsync(db, cv, Text.Tidy(request.CvText ?? ""), null, request, isNew: false, ct)
                : Results.NotFound());

        // Ersätter texten i ett CV med texten ur en uppladdad fil. Utan id skapas ett nytt CV.
        cvs.MapPost("/{id:int}/upload", async (int id, IFormFile file, JobbAgentDbContext db, CancellationToken ct) =>
            await db.Profiles.FindAsync([id], ct) is { } cv ? await UploadAsync(db, cv, file, isNew: false, ct) : Results.NotFound())
            .DisableAntiforgery();

        cvs.MapPost("/upload", async (IFormFile file, JobbAgentDbContext db, CancellationToken ct) =>
        {
            if (await db.Profiles.CountAsync(ct) >= MaxCvs)
                return Results.BadRequest(new { error = L($"Du kan ha högst {MaxCvs} CV.", $"You can have at most {MaxCvs} CVs.") });
            var cv = new Profile
            {
                Id = (await db.Profiles.MaxAsync(p => (int?)p.Id, ct) ?? 0) + 1,
                IsDefault = !await db.Profiles.AnyAsync(ct),
                Name = Text.Truncate(Path.GetFileNameWithoutExtension(file.FileName), 60),
            };
            return await UploadAsync(db, cv, file, isNew: true, ct);
        }).DisableAntiforgery();

        cvs.MapPost("/{id:int}/default", async (int id, JobbAgentDbContext db, CancellationToken ct) =>
        {
            var all = await db.Profiles.ToListAsync(ct);
            if (all.All(p => p.Id != id)) return Results.NotFound();
            all.ForEach(p => p.IsDefault = p.Id == id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        cvs.MapDelete("/{id:int}", async (int id, JobbAgentDbContext db, CancellationToken ct) =>
        {
            var all = await db.Profiles.ToListAsync(ct);
            var cv = all.FirstOrDefault(p => p.Id == id);
            if (cv is null) return Results.NotFound();
            if (all.Count == 1) return Results.BadRequest(new { error = L("Du måste ha minst ett CV.", "You must have at least one CV.") });
            db.Profiles.Remove(cv);
            if (cv.IsDefault) all.First(p => p.Id != id).IsDefault = true;
            // Bevakningar som använde CV:t går över till standard-CV:t.
            await db.Watches.Where(w => w.CvId == id).ForEachAsync(w => w.CvId = null, ct);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> UploadAsync(JobbAgentDbContext db, Profile cv, IFormFile file, bool isNew, CancellationToken ct)
    {
        if (file.Length > MaxFileSize)
            return Results.BadRequest(new { error = L("Filen är för stor (max 5 MB).", "The file is too large (max 5 MB).") });
        try
        {
            await using var stream = file.OpenReadStream();
            return await SaveAsync(db, cv, CvReader.ReadText(stream, file.FileName), Path.GetFileName(file.FileName), null, isNew, ct);
        }
        catch (DocumentException e)
        {
            return Results.BadRequest(new { error = e.Message });
        }
    }

    private static async Task<IResult> SaveAsync(JobbAgentDbContext db, Profile cv, string text, string? fileName, SaveCvRequest? details,
        bool isNew, CancellationToken ct)
    {
        if (text.Length < MinCvLength)
            return Results.BadRequest(new { error = L($"CV:t är för kort (minst {MinCvLength} tecken).", $"The CV is too short (at least {MinCvLength} characters).") });
        if (text.Length > MaxCvLength)
            return Results.BadRequest(new { error = L($"CV:t är för långt (högst {MaxCvLength} tecken).", $"The CV is too long (at most {MaxCvLength} characters).") });

        // En ny fil ersätter texten; sparas texten för hand ligger filnamnet kvar om texten inte ändrats.
        if (fileName is not null || cv.CvText != text) cv.FileName = fileName;
        cv.CvText = text;
        if (details is not null)
        {
            cv.Name = Clean(details.Name, 60) ?? cv.Name;
            cv.FullName = Clean(details.FullName, 100);
            cv.Email = Clean(details.Email, 200);
            cv.Phone = Clean(details.Phone, 50);
            cv.City = Clean(details.City, 100);
            cv.Link = Clean(details.Link, 300);
        }
        if (isNew && details is null && await Cvs.ResolveAsync(db, null, ct) is { } template)
        {
            // Ett nytt CV från fil får samma kontaktuppgifter som standard-CV:t.
            cv.FullName = template.FullName;
            cv.Email = template.Email;
            cv.Phone = template.Phone;
            cv.City = template.City;
            cv.Link = template.Link;
        }
        // Tomma kontaktuppgifter fylls i med det som går att hitta i CV:t.
        var guess = ContactInfo.Guess(text);
        cv.FullName ??= guess.FullName;
        cv.Email ??= guess.Email;
        cv.Phone ??= guess.Phone;
        cv.UpdatedAt = DateTime.UtcNow;
        if (isNew) db.Profiles.Add(cv);
        await db.SaveChangesAsync(ct);
        return isNew ? Results.Created($"/api/cvs/{cv.Id}", CvDto.From(cv)) : Results.Ok(CvDto.From(cv));
    }

    private static string? Clean(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : Text.Truncate(value.Trim(), max);
}
