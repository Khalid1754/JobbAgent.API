using System.Text.RegularExpressions;

namespace JobbAgent.Api.Services;

public record Contact(string? FullName, string? Email, string? Phone);

// Gissar namn, e-post och telefon ur CV-texten, så att brevhuvudet blir ifyllt från början.
public static partial class ContactInfo
{
    public static Contact Guess(string cv)
    {
        var email = Email().Match(cv) is { Success: true } e ? e.Value : null;
        var phone = Phone().Match(cv) is { Success: true } p ? Regex.Replace(p.Value.Trim(), @"\s+", " ") : null;
        // Namnet står nästan alltid först: två till fyra ord med bara bokstäver.
        var firstLine = cv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        var name = firstLine is not null && Name().IsMatch(firstLine) ? firstLine : null;
        return new Contact(name, email, phone);
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")]
    private static partial Regex Email();
    [GeneratedRegex(@"(\+46|0)[\s-]?7\d([\s-]?\d){7}|(\+46|0)[1-9]\d{0,2}([\s-]?\d){5,8}")]
    private static partial Regex Phone();
    [GeneratedRegex(@"^\p{Lu}[\p{L}'-]+( \p{L}[\p{L}'-]*){1,3}$")]
    private static partial Regex Name();
}
