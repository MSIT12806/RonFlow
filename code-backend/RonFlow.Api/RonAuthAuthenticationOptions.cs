namespace RonFlow.Api;

public sealed class RonAuthAuthenticationOptions
{
    public const string SectionName = "Authentication:RonAuth";

    public string Issuer { get; init; } = "RonAuth";
    public string Audience { get; init; } = "RonFlow.Client";
    public string SigningKey { get; init; } = "RonAuth-Development-Signing-Key-For-Local-Only-1234567890";
}