using System.Text.Json.Nodes;
using GrandPrix.Persistence;

namespace GrandPrix.Persistence.Tests;

public sealed class VersionedSaveSerializerTests
{
    [Fact]
    public void CurrentVersionRoundTripsPayload()
    {
        var serializer = new VersionedSaveSerializer(1);
        var bytes = serializer.Serialize(new SavePayload { Date = "2026-03-08", Points = 12 });

        var payload = serializer.Deserialize<SavePayload>(bytes);

        Assert.Equal("2026-03-08", payload.Date);
        Assert.Equal(12, payload.Points);
    }

    [Fact]
    public void AppliesEachRegisteredMigrationInOrder()
    {
        var serializer = new VersionedSaveSerializer(3, new ISaveMigration[]
        {
            new AddPointsMigration(),
            new RenameDateMigration()
        });
        var oldSave = System.Text.Encoding.UTF8.GetBytes(
            "{\"version\":1,\"payload\":{\"eventDate\":\"2026-03-08\"}}");

        var payload = serializer.Deserialize<SavePayload>(oldSave);

        Assert.Equal("2026-03-08", payload.Date);
        Assert.Equal(12, payload.Points);
    }

    [Fact]
    public void RejectsUnknownOrMalformedVersions()
    {
        var serializer = new VersionedSaveSerializer(1);

        Assert.Throws<InvalidDataException>(() =>
            serializer.Deserialize<SavePayload>(System.Text.Encoding.UTF8.GetBytes(
                "{\"version\":2,\"payload\":{}}")));
        Assert.Throws<InvalidDataException>(() =>
            serializer.Deserialize<SavePayload>(System.Text.Encoding.UTF8.GetBytes(
                "{\"version\":0,\"payload\":{}}")));
        Assert.Throws<InvalidDataException>(() =>
            serializer.Deserialize<SavePayload>(System.Text.Encoding.UTF8.GetBytes(
                "{\"payload\":{}}")));
    }

    [Fact]
    public void MissingMigrationIsReported()
    {
        var serializer = new VersionedSaveSerializer(2);
        var oldSave = System.Text.Encoding.UTF8.GetBytes(
            "{\"version\":1,\"payload\":{}}");

        var exception = Assert.Throws<InvalidDataException>(() =>
            serializer.Deserialize<SavePayload>(oldSave));

        Assert.Contains("No save migration", exception.Message, StringComparison.Ordinal);
    }

    public sealed class SavePayload
    {
        public string Date { get; set; } = string.Empty;
        public int Points { get; set; }
    }

    private sealed class AddPointsMigration : ISaveMigration
    {
        public int FromVersion => 1;

        public JsonNode Migrate(JsonNode payload)
        {
            payload["points"] = 12;
            return payload;
        }
    }

    private sealed class RenameDateMigration : ISaveMigration
    {
        public int FromVersion => 2;

        public JsonNode Migrate(JsonNode payload)
        {
            payload["date"] = payload["eventDate"]?.DeepClone();
            payload.AsObject().Remove("eventDate");
            return payload;
        }
    }
}
