namespace ObjectStorage.Infrastructure.Configuration;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string HeaderName { get; set; } = "X-Api-Key";
    public bool SkipInDevelopment { get; set; } = true;
}
