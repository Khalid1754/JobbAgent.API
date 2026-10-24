using System.Text.Json;
using System.Text.Json.Serialization;
using JobbAgent.Api.Agents;

namespace JobbAgent.Api.Models;

public record SaveCvRequest(string CvText);

public record ProfileDto(string CvText, string? FileName, DateTime UpdatedAt)
{
    public static ProfileDto From(Profile p) => new(p.CvText, p.FileName, p.UpdatedAt);
}

public record CreateSearchRequest(string? Location, string? Keywords, bool RemoteOnly);

// Annonsen anges på ett av tre sätt: inklistrad text, en länk eller ett Platsbanken-id från en sökning.
public record CreateLetterRequest(string? AdText, string? AdUrl, string? PlatsbankenId, string? Language);

public record UpdateLetterRequest(string Letter);

// Intervjuförberedelse utgår från ett brev eller från ett jobb på tavlan.
public record CreateInterviewRequest(Guid? LetterRunId, Guid? ApplicationId);

// Nytt jobb på tavlan. Kommer det från ett brev räcker LetterRunId, resten hämtas från brevet.
public record CreateApplicationRequest(
    string? Title, string? Company, string? Location, string? Url, string? PlatsbankenId,
    DateTime? Deadline, int? MatchScore, Guid? LetterRunId, Stage? Stage);

public record UpdateApplicationRequest(
    string Title, string? Company, string? Location, string? Url, DateTime? Deadline,
    Stage Stage, string? Notes, DateTime? AppliedAt, DateTime? InterviewAt);

public record ApplicationDto(
    Guid Id, string Title, string? Company, string? Location, string? Url, string? PlatsbankenId,
    DateTime? Deadline, int? MatchScore, Guid? LetterRunId, Stage Stage, string? Notes,
    DateTime? AppliedAt, DateTime? InterviewAt, DateTime CreatedAt, DateTime StageChangedAt)
{
    public static ApplicationDto From(JobApplication a) => new(
        a.Id, a.Title, a.Company, a.Location, a.Url, a.PlatsbankenId, a.Deadline, a.MatchScore, a.LetterRunId,
        a.Stage, a.Notes, a.AppliedAt, a.InterviewAt, a.CreatedAt, a.StageChangedAt);
}

public record RunSummaryDto(Guid Id, RunKind Kind, string Title, string? Company, RunStatus Status, int? MatchScore,
    Guid? SourceRunId, Guid? ApplicationId, DateTime CreatedAt, DateTime? FinishedAt)
{
    public static RunSummaryDto From(Run r) =>
        new(r.Id, r.Kind, r.Title, r.Company, r.Status, r.MatchScore, r.SourceRunId, r.ApplicationId, r.CreatedAt, r.FinishedAt);
}

public record StepDto(int Sequence, string Agent, string? ToAgent, StepKind Kind, string Summary, string? Detail, DateTime CreatedAt)
{
    public static StepDto From(AgentStep s) => new(s.Sequence, s.Agent, s.ToAgent, s.Kind, s.Summary, s.Detail, s.CreatedAt);
}

public record RunDto(
    Guid Id, RunKind Kind, string Title, string? Company, RunStatus Status, int? MatchScore,
    string? Location, string? Keywords, bool RemoteOnly,
    string? AdUrl, string? AdText, string? PlatsbankenId, string Language,
    IReadOnlyList<SearchHit>? Hits, AdRequirements? Requirements, MatchResult? Match, string? Letter,
    InterviewPrep? Interview, Guid? SourceRunId, Guid? ApplicationId, string? Error, DateTime CreatedAt, DateTime? FinishedAt, IReadOnlyList<StepDto> Steps)
{
    public static RunDto From(Run r) => new(
        r.Id, r.Kind, r.Title, r.Company, r.Status, r.MatchScore,
        r.Location, r.Keywords, r.RemoteOnly,
        r.AdUrl, r.AdText, r.PlatsbankenId, r.Language,
        Read<List<SearchHit>>(r.HitsJson), Read<AdRequirements>(r.RequirementsJson), Read<MatchResult>(r.MatchJson), r.Letter,
        Read<InterviewPrep>(r.InterviewJson), r.SourceRunId, r.ApplicationId, r.Error, r.CreatedAt, r.FinishedAt,
        r.Steps.OrderBy(s => s.Sequence).Select(StepDto.From).ToList());

    private static T? Read<T>(string? json) where T : class =>
        json is null ? null : JsonSerializer.Deserialize<T>(json, ApiJson.Options);
}

// Det som skickas live till webbläsaren: antingen ett nytt steg eller en ny status för körningen.
public record RunEvent(string Type, StepDto? Step = null, RunStatus? Status = null, string? Error = null)
{
    public static RunEvent ForStep(AgentStep s) => new("step", Step: StepDto.From(s));
    public static RunEvent ForStatus(Run r) => new("status", Status: r.Status, Error: r.Error);
}

public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions o)
    {
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        return o;
    }
}
