using System.ComponentModel;

namespace JobbAgent.Api.Agents;

// Det agenterna svarar med. Beskrivningarna skickas med i JSON-schemat till AI:n.

public record SearchPlan(
    [property: Description("1–3 korta sökfraser för Platsbanken, t.ex. \"systemutvecklare .NET\". Yrke och viktigaste kompetens, inte hela meningar.")] string[] Queries,
    [property: Description("En mening om vilken typ av jobb som passar CV:t.")] string Summary);

public record RankedAd(
    [property: Description("Annonsens nummer i listan.")] int Index,
    [property: Description("Hur väl CV:t passar annonsen, 0–100.")] int Score,
    [property: Description("En kort mening om varför, på svenska.")] string Reason);

public record Ranking(RankedAd[] Ads);

// En annons i sökresultatet, med Matcharens bedömning.
public record SearchHit(
    string Id, string Title, string? Employer, string? Municipality, string Url,
    DateTime? Deadline, int Score, string Reason);

public record AdRequirements(
    [property: Description("Tjänstens titel.")] string Title,
    [property: Description("Arbetsgivarens namn, tom sträng om det inte står.")] string Company,
    [property: Description("Ort, tom sträng om det inte står.")] string Location,
    [property: Description("Två–tre meningar om vad jobbet går ut på.")] string Summary,
    [property: Description("Krav som måste uppfyllas.")] string[] MustHave,
    [property: Description("Meriterande, inte krav.")] string[] NiceToHave,
    [property: Description("Arbetsuppgifter.")] string[] Responsibilities,
    [property: Description("Personliga egenskaper de söker.")] string[] Traits,
    [property: Description("Kontaktperson att ställa brevet till, tom sträng om ingen nämns.")] string ContactPerson,
    [property: Description("Annonsens språk: \"sv\" eller \"en\".")] string Language);

public record RequirementMatch(
    [property: Description("Kravet ur annonsen.")] string Requirement,
    [property: Description("Vad i CV:t som visar det. Bara sådant som faktiskt står i CV:t.")] string Evidence);

public record CvTip(
    [property: Description("Vilken del av CV:t det gäller, t.ex. \"Profil\", \"Erfarenhet\" eller \"Kompetenser\".")] string Section,
    [property: Description("Vad som ska ändras och varför, kopplat till ett krav i annonsen.")] string Suggestion,
    [property: Description("Förslag på ny formulering, byggd bara på det som redan står i CV:t. Tom sträng om det inte passar.")] string Example);

public record MatchResult(
    [property: Description("Hur väl CV:t passar annonsen, 0–100.")] int Score,
    [property: Description("Två meningar om matchningen.")] string Summary,
    RequirementMatch[] Matches,
    [property: Description("Krav som CV:t inte visar.")] string[] Gaps,
    [property: Description("Det brevet bör lyfta fram, viktigast först.")] string[] Highlights,
    [property: Description("3–5 sätt att anpassa CV:t för just det här jobbet.")] CvTip[] CvTips);

public record Review(
    bool Approved,
    [property: Description("Konkreta saker Skribenten ska rätta. Tom sträng om brevet är godkänt.")] string Feedback);
