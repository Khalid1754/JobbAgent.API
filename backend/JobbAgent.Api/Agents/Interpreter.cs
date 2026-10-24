using JobbAgent.Api.Services;

namespace JobbAgent.Api.Agents;

// Tolkare: läser annonsen och plockar ut krav, meriter, arbetsuppgifter och vad de söker hos en person.
public class Interpreter(ILlm llm)
{
    public const string Name = "Tolkare";

    private const string System = """
        You are Tolkare in a team of AI agents that help a job seeker. Read the job ad and extract
        what the employer asks for. Keep each list item short (a few words). Separate hard
        requirements from merits. Write the lists in the ad's language. Ignore cookie banners,
        menus and other page noise.
        """;

    public Task<AdRequirements> InterpretAsync(string adText, CancellationToken ct) =>
        llm.CompleteJsonAsync<AdRequirements>(System, $"""
            Job ad:
            {Text.Truncate(adText, AdReader.MaxLength)}
            """, ct);

    public static string Format(AdRequirements r) => $"""
        **{r.Title}**{(r.Company.Length > 0 ? $" · {r.Company}" : "")}{(r.Location.Length > 0 ? $" · {r.Location}" : "")}

        {r.Summary}

        {List("Krav", r.MustHave)}{List("Meriterande", r.NiceToHave)}{List("Arbetsuppgifter", r.Responsibilities)}{List("Personliga egenskaper", r.Traits)}
        """.Trim();

    private static string List(string heading, string[] items) =>
        items.Length == 0 ? "" : $"**{heading}**\n{string.Join("\n", items.Select(i => $"- {i}"))}\n\n";
}
