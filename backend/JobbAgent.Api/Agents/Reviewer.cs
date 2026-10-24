using JobbAgent.Api.Services;

namespace JobbAgent.Api.Agents;

// Granskare: kontrollerar brevet mot CV:t och annonsen och kan skicka tillbaka det till Skribenten.
public class Reviewer(ILlm llm)
{
    public const string Name = "Granskare";

    private const string System = """
        You are Granskare in a team of AI agents that help a job seeker. Check the cover letter
        against the CV and the job. Reject it only for real problems:
        - it claims experience, employers, education, tools or results that are not in the CV
        - it misses the job's most important requirements that the CV does support
        - it is in the wrong language, has placeholders like [Namn], or is far from 250–400 words
        Otherwise approve it. Write feedback as a short list of concrete fixes, in the language the user asks for.
        """;

    public Task<Review> ReviewAsync(string cv, AdRequirements requirements, string language, string letter, CancellationToken ct) =>
        llm.CompleteJsonAsync<Review>(System, $"""
            Required language of the letter: {Writer.LanguageName(language)}
            Write your feedback in {Language.AgentLanguage}.

            CV:
            {Text.Truncate(cv, 8000)}

            Job:
            {Interpreter.Format(requirements)}

            Cover letter:
            {letter}
            """, ct);
}
