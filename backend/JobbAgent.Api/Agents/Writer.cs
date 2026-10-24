using JobbAgent.Api.Services;

namespace JobbAgent.Api.Agents;

// Skribent: skriver det personliga brevet, och skriver om det när Granskaren har synpunkter.
public class Writer(ILlm llm)
{
    public const string Name = "Skribent";

    private const string System = """
        You are Skribent in a team of AI agents that help a job seeker. Write a personal cover letter
        (personligt brev) for the job, based on the CV and Matchare's analysis.
        Rules:
        - Use only facts that are in the CV. Never invent employers, years, degrees, tools or results.
        - 250–400 words, 3–5 short paragraphs, warm and professional, no clichés.
        - Open with why this particular role and employer, then connect the CV to the most important
          requirements with concrete examples, then a short closing with a wish to meet.
        - Address the contact person by name if one is given; otherwise use a neutral greeting.
        - Sign with the applicant's name from the CV. Never leave placeholders like [Namn] or [Företag];
          leave out anything you do not know.
        - Plain text only, no Markdown, no subject line.
        Output only the letter.
        """;

    public async Task<string> WriteAsync(string cv, AdRequirements requirements, MatchResult match, string language,
        string? previousLetter, string? feedback, CancellationToken ct)
    {
        var revision = feedback is null ? "" : $"""

            Your previous draft:
            {previousLetter}

            Granskare's feedback, fix all of it:
            {feedback}
            """;
        var letter = await llm.CompleteAsync(System, $"""
            Write the letter in {LanguageName(language)}.

            CV:
            {Text.Truncate(cv, 8000)}

            Job:
            {Interpreter.Format(requirements)}
            Contact person: {(requirements.ContactPerson.Length > 0 ? requirements.ContactPerson : "unknown")}

            Matchare's analysis:
            {Matcher.Format(match, withCvTips: false)}
            {revision}
            """, null, ct);
        return letter.Trim();
    }

    public static string LanguageName(string language) => language == "en" ? "English" : "Swedish";
}
