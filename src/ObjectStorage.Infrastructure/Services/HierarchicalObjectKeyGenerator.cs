using System.Text;
using ObjectStorage.Application.Interfaces;

namespace ObjectStorage.Infrastructure.Services;

public sealed class HierarchicalObjectKeyGenerator : IObjectKeyGenerator
{
    public string Generate(string service, string category, string objectId, string fileName)
    {
        var sanitized = SanitizeFileName(fileName);
        return $"{service.Trim().ToLowerInvariant()}/{category.Trim().ToLowerInvariant()}/{objectId}/{sanitized}";
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "file";
        }

        var builder = new StringBuilder(fileName.Length);
        foreach (var c in fileName)
        {
            if (c == '/' || c == '\\' || char.IsControl(c))
            {
                continue;
            }

            builder.Append(c);
        }

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrEmpty(sanitized) ? "file" : sanitized;
    }
}
