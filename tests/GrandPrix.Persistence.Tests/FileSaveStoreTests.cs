using GrandPrix.Persistence;

namespace GrandPrix.Persistence.Tests;

public sealed class FileSaveStoreTests
{
    [Fact]
    public void SaveLoadAndListRoundTripSlots()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"grand-prix-save-{Guid.NewGuid():N}");
        try
        {
            var store = new FileSaveStore(directory);
            var first = new byte[] { 1, 2, 3 };
            var second = new byte[] { 4, 5 };

            store.Save("career_1", first);
            store.Save("career-2", second);

            Assert.Equal(first, store.Load("career_1"));
            Assert.Equal(second, store.Load("career-2"));
            Assert.Equal(new[] { "career-2", "career_1" }, store.ListSlots());
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SavingAgainAtomicallyReplacesSlotData()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"grand-prix-save-{Guid.NewGuid():N}");
        try
        {
            var store = new FileSaveStore(directory);
            store.Save("career", new byte[] { 1 });
            store.Save("career", new byte[] { 2, 3 });

            Assert.Equal(new byte[] { 2, 3 }, store.Load("career"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("../outside")]
    [InlineData("save/name")]
    public void RejectsUnsafeSlotNames(string slot)
    {
        var store = new FileSaveStore(Path.GetTempPath());

        Assert.Throws<ArgumentException>(() => store.Load(slot));
    }

    [Fact]
    public void MissingSlotIsReported()
    {
        var store = new FileSaveStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Throws<FileNotFoundException>(() => store.Load("missing"));
    }
}
