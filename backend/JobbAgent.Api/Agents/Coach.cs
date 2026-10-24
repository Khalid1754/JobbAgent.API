using System.ComponentModel;
using JobbAgent.Api.Services;

namespace JobbAgent.Api.Agents;

public record InterviewQuestion(
    [property: Description("Kategori: \"Om dig\", \"Kompetens\", \"Situation\" eller \"Om jobbet\" (på engelska: \"About you\", \"Skills\", \"Situational\", \"About the job\").")] string Category,
    [property: Description("Frågan som den skulle ställas.")] string Question,
    [property: Description("En mening om vad arbetsgivaren vill ta reda på.")] string WhyTheyAsk,
    [property: Description("2–4 korta tips på vad svaret ska innehålla, med konkreta exempel ur CV:t.")] string[] Tips,
    [property: Description("Ett kort exempelsvar i jag-form (3–5 meningar) som bara bygger på det som står i CV:t.")] string ExampleAnswer);

public record GapAdvice(
    [property: Description("Det som saknas i CV:t jämfört med annonsen.")] string Gap,
    [property: Description("Hur man ärligt bemöter det om frågan kommer.")] string HowToHandle);

public record InterviewPrep(
    [property: Description("Två–tre meningar med de viktigaste råden inför just den här intervjun.")] string Summary,
    InterviewQuestion[] Questions,
    [property: Description("3–5 bra frågor att själv ställa till arbetsgivaren.")] string[] QuestionsToAsk,
    GapAdvice[] Gaps);

// Intervjucoach: tar fram troliga intervjufrågor och förslag på svar utifrån annonsen och CV:t.
public class Coach(ILlm llm)
{
    public const string Name = "Intervjucoach";

    private const string System = """
        You are Intervjucoach in a team of AI agents that help a job seeker prepare for a job
        interview. Based on the job and the CV, list the 8–10 questions this employer is most likely
        to ask: a couple about the person, the rest about the job's key requirements and situations
        (behavioural questions). For each, give answer tips and a short example answer in first
        person using the STAR method where it fits. Use only facts from the CV; never invent
        experience. For requirements the CV lacks, advise how to handle them honestly (transferable
        skills, willingness to learn). Add good questions to ask the employer.
        """;

    public Task<InterviewPrep> PrepareAsync(string cv, AdRequirements requirements, MatchResult match, string language, CancellationToken ct) =>
        llm.CompleteJsonAsync<InterviewPrep>(System, $"""
            Write everything in {Writer.LanguageName(language)}.

            CV:
            {Text.Truncate(cv, 8000)}

            Job:
            {Interpreter.Format(requirements)}

            Matchare's analysis:
            {Matcher.Format(match, withCvTips: false)}
            """, ct);

    public static string Format(InterviewPrep p) =>
        $"{p.Summary}\n\n" + string.Join("\n", p.Questions.Select((q, i) => $"{i + 1}. **{q.Question}** ({q.Category})"));
}
