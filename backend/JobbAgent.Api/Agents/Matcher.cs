using JobbAgent.Api.Services;
using static JobbAgent.Api.Services.Language;

namespace JobbAgent.Api.Agents;

// Matchare: jämför CV:t med annonser. Rankar sökträffar, eller går igenom en annons krav för krav.
public class Matcher(ILlm llm)
{
    public const string Name = "Matchare";

    private const string RankSystem = """
        You are Matchare in a team of AI agents that help a job seeker. Score how well the CV fits
        each job ad from 0 to 100 (70+ = strong fit, 40–69 = possible, below 40 = poor fit). Be
        honest: required experience or education the CV lacks lowers the score. Score every ad.
        Write each reason as one short sentence in the language the user asks for.
        """;

    private const string MatchSystem = """
        You are Matchare in a team of AI agents that help a job seeker. Compare the CV with the
        job's requirements. For each requirement the CV supports, quote or paraphrase the evidence
        from the CV; never invent experience. List requirements the CV does not show as gaps. Score
        the fit 0–100 and list what a cover letter should highlight, most important first.
        Also suggest 3–5 concrete ways to tailor the CV for this job: what to move up, rephrase with
        the ad's own words, or make more visible. Only rearrange and rephrase what the CV already
        says; never suggest adding experience the person does not have. Write in the language the user asks for.
        """;

    public const int DescriptionLength = 700;

    public async Task<List<SearchHit>> RankAsync(string cv, IReadOnlyList<JobAd> ads, CancellationToken ct)
    {
        if (ads.Count == 0) return [];
        var list = string.Join("\n\n", ads.Select((ad, i) => $"""
            [{i + 1}] {ad.Title} · {ad.Employer ?? L("okänd arbetsgivare", "unknown employer")} · {ad.Municipality ?? L("okänd ort", "unknown location")}
            {Text.Truncate(Text.Tidy(ad.Description), DescriptionLength)}
            """));
        var ranking = await llm.CompleteJsonAsync<Ranking>(RankSystem, $"""
            Write the reasons in {Language.AgentLanguage}.

            CV:
            {Text.Truncate(cv, 6000)}

            Job ads:
            {list}
            """, ct);

        // Annonser som AI:n hoppade över hamnar sist med poäng 0 i stället för att försvinna.
        var scores = ranking.Ads.Where(r => r.Index >= 1 && r.Index <= ads.Count)
            .GroupBy(r => r.Index).ToDictionary(g => g.Key, g => g.First());
        return ads.Select((ad, i) =>
            {
                var r = scores.GetValueOrDefault(i + 1);
                return new SearchHit(ad.Id, ad.Title, ad.Employer, ad.Municipality, ad.Url, ad.Deadline,
                    Math.Clamp(r?.Score ?? 0, 0, 100), r?.Reason ?? L("Ingen bedömning.", "No assessment."), Source: ad.Source);
            })
            .OrderByDescending(h => h.Score)
            .ToList();
    }

    public async Task<MatchResult> MatchAsync(string cv, AdRequirements requirements, CancellationToken ct)
    {
        var match = await llm.CompleteJsonAsync<MatchResult>(MatchSystem, $"""
            Write the summary, evidence, gaps, highlights and CV tips in {Language.AgentLanguage}.

            CV:
            {Text.Truncate(cv, 8000)}

            Job:
            {Interpreter.Format(requirements)}
            """, ct);
        return match with { Score = Math.Clamp(match.Score, 0, 100) };
    }

    // CV-tipsen visas för användaren men behövs inte av Skribenten och Intervjucoachen.
    public static string Format(MatchResult m, bool withCvTips = true) => $"""
        **{L("Matchning", "Match")}: {m.Score} %**

        {m.Summary}

        {(m.Matches.Length > 0 ? L("**Det här passar**\n", "**What fits**\n") + string.Join("\n", m.Matches.Select(x => $"- {x.Requirement}: {x.Evidence}")) + "\n\n" : "")}{(m.Gaps.Length > 0 ? L("**Det här saknas**\n", "**What is missing**\n") + string.Join("\n", m.Gaps.Select(g => $"- {g}")) + "\n\n" : "")}{(m.Highlights.Length > 0 ? L("**Lyft fram i brevet**\n", "**Highlight in the letter**\n") + string.Join("\n", m.Highlights.Select(h => $"- {h}")) + "\n\n" : "")}{(withCvTips ? FormatTips(m.CvTips ?? []) : "")}
        """.Trim();

    private static string FormatTips(CvTip[] tips) => tips.Length == 0 ? "" :
        L("**Anpassa CV:t**\n", "**Tailor the CV**\n") + string.Join("\n", tips.Select(t => $"- {t.Section}: {t.Suggestion}" + (t.Example.Length > 0 ? $" (\"{t.Example}\")" : "")));

    public static string FormatHits(IReadOnlyList<SearchHit> hits) => hits.Count == 0
        ? L("Inga annonser hittades.", "No ads found.")
        : string.Join("\n", hits.Select((h, i) => $"{i + 1}. **{h.Score} %** [{h.Title}]({h.Url}) · {h.Employer ?? L("okänd", "unknown")}: {h.Reason}"));
}
