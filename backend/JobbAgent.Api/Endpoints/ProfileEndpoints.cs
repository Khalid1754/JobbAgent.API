using JobbAgent.Api.Data;
using JobbAgent.Api.Models;
using JobbAgent.Api.Services;

namespace JobbAgent.Api.Endpoints;

public static class ProfileEndpoints
{
    public const int MinCvLength = 50;
    public const int MaxCvLength = 30000;
    public const long MaxFileSize = 5 * 1024 * 1024;

    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var profile = app.MapGroup("/api/profile");

        profile.MapGet("/", async (JobbAgentDbContext db, CancellationToken ct) =>
            await db.Profiles.FindAsync([1], ct) is { } p ? Results.Ok(ProfileDto.From(p)) : Results.NotFound());

        profile.MapPut("/", async (SaveCvRequest request, JobbAgentDbContext db, CancellationToken ct) =>
            await SaveAsync(db, Text.Tidy(request.CvText ?? ""), null, ct));

        profile.MapPost("/upload", async (IFormFile file, JobbAgentDbContext db, CancellationToken ct) =>
        {
            if (file.Length > MaxFileSize)
                return Results.BadRequest(new { error = "Filen är för stor (max 5 MB)." });
            try
            {
                await using var stream = file.OpenReadStream();
                return await SaveAsync(db, CvReader.ReadText(stream, file.FileName), Path.GetFileName(file.FileName), ct);
            }
            catch (DocumentException e)
            {
                return Results.BadRequest(new { error = e.Message });
            }
        }).DisableAntiforgery();
    }

    private static async Task<IResult> SaveAsync(JobbAgentDbContext db, string cv, string? fileName, CancellationToken ct)
    {
        if (cv.Length < MinCvLength)
            return Results.BadRequest(new { error = $"CV:t är för kort (minst {MinCvLength} tecken)." });
        if (cv.Length > MaxCvLength)
            return Results.BadRequest(new { error = $"CV:t är för långt (högst {MaxCvLength} tecken)." });

        var profile = await db.Profiles.FindAsync([1], ct);
        if (profile is null) db.Profiles.Add(profile = new Profile());
        profile.CvText = cv;
        profile.FileName = fileName;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ProfileDto.From(profile));
    }
}
