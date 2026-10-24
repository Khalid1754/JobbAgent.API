namespace JobbAgent.Api.Services;

public enum Lang { Sv, En }

// Språket i gränssnittet. Appen skickar valt språk i Accept-Language ("sv" eller "en"); svenska om inget anges.
// Språket gäller för hela anropet, och för en körning i bakgrunden sätts det från körningens sparade språk.
public static class Language
{
    private static readonly AsyncLocal<Lang> current = new();

    public static Lang Current
    {
        get => current.Value;
        set => current.Value = value;
    }

    public static Lang Parse(string? value) =>
        value?.TrimStart().StartsWith("en", StringComparison.OrdinalIgnoreCase) == true ? Lang.En : Lang.Sv;

    public static string Code(this Lang lang) => lang == Lang.En ? "en" : "sv";

    // Väljer den svenska eller engelska texten utifrån det aktuella språket.
    public static string L(string sv, string en) => Current == Lang.En ? en : sv;

    // Vilket språk agenterna ska skriva sina analyser på.
    public static string AgentLanguage => Current == Lang.En ? "English" : "Swedish";
}
