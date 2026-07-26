using System.Text.Json;

namespace Kart.Search.Infrastructure.Messaging;

/// <summary>
/// Loads and parses <c>contracts/message-bus-manifest.json</c>. Fails fast at startup - a missing
/// or malformed manifest means the topology it describes cannot exist, so there is no safe
/// degraded mode to fall back to (kart-identity-service's proven pattern).
/// </summary>
public static class MessageBusManifestLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static MessageBusManifest Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Message bus manifest not found at '{path}'.", path);
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MessageBusManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Message bus manifest at '{path}' deserialized to null.");
    }
}
