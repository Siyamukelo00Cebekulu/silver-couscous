using System.Collections.Generic;

namespace GrandPrix.Core.Abstractions;

public interface ISaveStore
{
    void Save(string slot, byte[] data);
    byte[] Load(string slot);
    IReadOnlyList<string> ListSlots();
}
