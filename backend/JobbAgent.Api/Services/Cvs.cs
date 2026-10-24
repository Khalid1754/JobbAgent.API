using JobbAgent.Api.Data;
using JobbAgent.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace JobbAgent.Api.Services;

public static class Cvs
{
    // Det CV som en körning ska använda: det valda, annars standard-CV:t.
    public static async Task<Profile?> ResolveAsync(JobbAgentDbContext db, int? cvId, CancellationToken ct) =>
        (cvId is { } id ? await db.Profiles.FindAsync([id], ct) : null)
        ?? await db.Profiles.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Id).FirstOrDefaultAsync(ct);

    // Kontrollerar ett valt CV när en körning startas. Null = allt ok.
    public static async Task<string?> CheckAsync(JobbAgentDbContext db, int? cvId, CancellationToken ct) =>
        !await db.Profiles.AnyAsync(ct) ? "Lägg in ditt CV först."
        : cvId is { } id && !await db.Profiles.AnyAsync(p => p.Id == id, ct) ? "Det valda CV:t finns inte."
        : null;
}
