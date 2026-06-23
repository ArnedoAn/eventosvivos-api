using System.Text.RegularExpressions;
using EventosVivos.Domain.Common;

namespace EventosVivos.Domain.ValueObjects;

public sealed record Email(string Value)
{
    private static readonly Regex EmailRegex = new(
        @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static Result<Email> Create(string raw)
    {
        var normalized = raw?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(normalized) || !EmailRegex.IsMatch(normalized))
            return Result.Failure<Email>(new Error("Email.InvalidFormat", "Email format is invalid."));

        return new Email(normalized);
    }
}
