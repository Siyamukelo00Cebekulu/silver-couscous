using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace GrandPrix.Persistence;

public interface ISaveMigration
{
    int FromVersion { get; }
    JsonNode Migrate(JsonNode payload);
}

public sealed class VersionedSaveSerializer
{
    private readonly int _currentVersion;
    private readonly IReadOnlyDictionary<int, ISaveMigration> _migrations;
    private readonly JsonSerializerOptions _options;

    public VersionedSaveSerializer(
        int currentVersion,
        IEnumerable<ISaveMigration>? migrations = null,
        JsonSerializerOptions? options = null)
    {
        if (currentVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(currentVersion));

        _currentVersion = currentVersion;
        _options = options ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _migrations = (migrations ?? Array.Empty<ISaveMigration>())
            .ToDictionary(migration => migration.FromVersion);
        if (_migrations.Keys.Any(version => version < 1 || version >= currentVersion))
            throw new ArgumentException(
                "Migration source versions must be between 1 and the current version minus one.",
                nameof(migrations));
    }

    public byte[] Serialize<T>(T payload)
    {
        var envelope = new SaveEnvelope<T>
        {
            Version = _currentVersion,
            Payload = payload
        };
        return JsonSerializer.SerializeToUtf8Bytes(envelope, _options);
    }

    public T Deserialize<T>(byte[] data)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(data);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Save data is not valid JSON.", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("version", out var versionElement) ||
                !versionElement.TryGetInt32(out var version) ||
                !root.TryGetProperty("payload", out var payloadElement))
                throw new InvalidDataException("Save data is missing its version or payload.");
            if (version < 1)
                throw new InvalidDataException($"Save data version {version} is not supported.");
            if (version > _currentVersion)
                throw new InvalidDataException(
                    $"Save data version {version} is newer than supported version {_currentVersion}.");

            var payload = JsonNode.Parse(payloadElement.GetRawText()) ??
                throw new InvalidDataException("Save data payload cannot be null.");
            while (version < _currentVersion)
            {
                if (!_migrations.TryGetValue(version, out var migration))
                    throw new InvalidDataException(
                        $"No save migration is registered from version {version}.");

                payload = migration.Migrate(payload) ??
                    throw new InvalidDataException(
                        $"Save migration from version {version} returned null.");
                version++;
            }

            try
            {
                return payload.Deserialize<T>(_options) ??
                    throw new InvalidDataException("Save data payload could not be deserialized.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Save data payload has an invalid shape.", exception);
            }
        }
    }

    private sealed class SaveEnvelope<T>
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("payload")]
        public T Payload { get; set; } = default!;
    }
}
