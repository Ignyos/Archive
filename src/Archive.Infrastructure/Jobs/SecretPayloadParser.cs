using System.Text.Json;

namespace Archive.Infrastructure.Jobs;

internal static class SecretPayloadParser
{
    public static string? TryReadStringField(string payload, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(fieldName))
        {
            return null;
        }

        var trimmed = payload.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.ToString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
