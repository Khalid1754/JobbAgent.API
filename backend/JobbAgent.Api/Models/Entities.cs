namespace JobbAgent.Api.Models;

public enum RunStatus { Queued, Running, Completed, Failed }

// Search = agenterna letar jobb i Platsbanken. Letter = agenterna skriver ett personligt brev till en annons.
// Interview = agenterna förbereder dig inför en intervju.
public enum RunKind { Search, Letter, Interview }

// Vad som hände i ett steg: en agent börjar, blir klar, lämnar över till nästa eller misslyckas.
public enum StepKind { Started, Completed, Handoff, Failed }

// En version av ditt CV, t.ex. ett för utvecklarjobb och ett för andra jobb. Ett av dem är standard.
public class Profile
{
    public int Id { get; set; } = 1;
    public string Name { get; set; } = "Mitt CV";
    public bool IsDefault { get; set; }
    public string CvText { get; set; } = "";
    public string? FileName { get; set; }
    // Kontaktuppgifter överst i brevet när det laddas ner som Word eller PDF.
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Link { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// En körning: antingen en jobbsökning eller ett personligt brev.
public class Run
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public RunKind Kind { get; set; }
    public RunStatus Status { get; set; } = RunStatus.Queued;
    // Rubriken i historiken: annonsens titel eller vad som söktes.
    public string Title { get; set; } = "";
    public string? Company { get; set; }

    // Sökning: önskemål från användaren.
    public string? Location { get; set; }
    public string? Keywords { get; set; }
    public bool RemoteOnly { get; set; }

    // Vilket CV agenterna använder (null = standard-CV:t).
    public int? CvId { get; set; }

    // Brev: själva annonsen och vilket språk brevet ska skrivas på (auto, sv eller en).
    public string? AdUrl { get; set; }
    public string? AdText { get; set; }
    public string? PlatsbankenId { get; set; }
    public string Language { get; set; } = "auto";
    // Gränssnittets språk när körningen startades; agenternas steg och analyser skrivs på det.
    public string UiLanguage { get; set; } = "sv";

    // Intervjuförberedelse: brevet den bygger på och jobbet på tavlan den gjordes för.
    public Guid? SourceRunId { get; set; }
    public Guid? ApplicationId { get; set; }

    // Sökningar som en bevakning startat.
    public Guid? WatchId { get; set; }

    // Agenternas resultat, sparat som JSON.
    public string? HitsJson { get; set; }
    public string? RequirementsJson { get; set; }
    public string? MatchJson { get; set; }
    public string? InterviewJson { get; set; }
    public int? MatchScore { get; set; }
    public string? Letter { get; set; }

    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public List<AgentStep> Steps { get; set; } = [];
}

// Var en ansökan befinner sig på tavlan.
public enum Stage { Saved, Applied, Interview, Offer, Rejected }

// Ett jobb på ansökningstavlan. Kan komma från en sökträff, ett brev eller läggas in för hand.
public class JobApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? Url { get; set; }
    public string? PlatsbankenId { get; set; }
    public DateTime? Deadline { get; set; }
    public int? MatchScore { get; set; }
    // Brevet som skrevs till jobbet, om det finns ett.
    public Guid? LetterRunId { get; set; }
    public Stage Stage { get; set; } = Stage.Saved;
    public string? Notes { get; set; }
    public DateTime? AppliedAt { get; set; }
    public DateTime? InterviewAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime StageChangedAt { get; set; } = DateTime.UtcNow;
}

// En sparad sökning som körs automatiskt varje morgon och visar nya annonser som matchar bra.
public class Watch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Location { get; set; }
    public string? Keywords { get; set; }
    public bool RemoteOnly { get; set; }
    public int MinScore { get; set; } = 70;
    public int? CvId { get; set; }
    public bool Enabled { get; set; } = true;
    // Annonser som redan visats, så att bara nya räknas nästa gång (JSON-lista med Platsbanken-id).
    public string SeenAdIdsJson { get; set; } = "[]";
    public DateTime? LastRunAt { get; set; }
    public Guid? LastRunId { get; set; }
    // Nya annonser över MinScore i senaste körningen som du inte har tittat på än.
    public int NewMatches { get; set; }
    public string UiLanguage { get; set; } = "sv";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AgentStep
{
    public int Id { get; set; }
    public Guid RunId { get; set; }
    public int Sequence { get; set; }
    public string Agent { get; set; } = "";
    public string? ToAgent { get; set; }
    public StepKind Kind { get; set; }
    public string Summary { get; set; } = "";
    // Det agenten producerade (Markdown), visas när man fäller ut steget.
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
