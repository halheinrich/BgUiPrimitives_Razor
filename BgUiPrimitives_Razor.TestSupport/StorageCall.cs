namespace BgUiPrimitives_Razor.TestSupport;

/// <summary>The three operations of <see cref="BrowserStorage"/>.</summary>
internal enum StorageOperation
{
    Read,
    Write,
    Remove,
}

/// <summary>
/// One storage call in the accessor's vocabulary — what a test expects, or
/// what the code under test made: the operation, the area, the key, and for a
/// write the value written.
/// </summary>
internal readonly record struct StorageCall(
    StorageOperation Operation, BrowserStorageArea Area, string Key, string? Value)
{
    public static StorageCall Read(BrowserStorageArea area, string key) =>
        new(StorageOperation.Read, area, key, Value: null);

    public static StorageCall Write(BrowserStorageArea area, string key, string value) =>
        new(StorageOperation.Write, area, key, value);

    public static StorageCall Remove(BrowserStorageArea area, string key) =>
        new(StorageOperation.Remove, area, key, Value: null);

    /// <summary>Whether the two name the same area and key.</summary>
    public bool SameItemAs(StorageCall other) => Area == other.Area && Key == other.Key;

    public string Item => $"{Area} '{Key}'";

    /// <summary>The description of a held expectation of this call.</summary>
    public string Held => $"the held {Noun}";

    public override string ToString() => $"a {Noun}";

    private string Noun => Operation switch
    {
        StorageOperation.Read => $"read of {Item}",
        StorageOperation.Write => $"write of '{Value}' to {Item}",
        _ => $"removal of {Item}",
    };
}
