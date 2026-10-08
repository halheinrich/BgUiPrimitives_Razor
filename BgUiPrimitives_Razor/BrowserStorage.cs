using Microsoft.JSInterop;

namespace BgUiPrimitives_Razor;

/// <summary>
/// The one guarded access to the browser's Web Storage — <c>localStorage</c>
/// and <c>sessionStorage</c> — for every member and host. Each operation makes
/// one call to the browser, and its result says how the browser answered: a
/// read finds a stored value, nothing stored, or a refusal; a write or a
/// removal succeeds or is refused. A refusal is a result, never an exception.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every requested operation is attempted.</b> The type holds no state
/// between calls: a refusal disables nothing — not later reads, not later
/// writes, not the other area, not the caller's in-memory value. Nor is a
/// refused call retried; the next call the caller makes is simply made. A
/// browser can refuse writes while it serves reads (a full quota), and refuse
/// one area and not the other, so a refusal of one call is never taken as an
/// answer for another.
/// </para>
/// <para>
/// <b>Only the browser's refusal is caught.</b> A browser that blocks storage
/// raises an error from the call, which reaches .NET as a
/// <see cref="JSException"/>; that, and only that, becomes a refused result,
/// carrying the exception as raised. Everything else propagates unchanged —
/// the platform's own refusal to make an interop call at all (during static
/// rendering, or once a server circuit has gone), a timeout, an argument
/// error. Those are not answers from the browser.
/// </para>
/// <para>
/// <b>Strings only.</b> Keys and values are the browser's strings, passed
/// through unchanged. Serializing, parsing, choosing a default, and judging
/// whether a stored value is valid stay with each caller, so a caller's parse
/// failure is never mistaken for a storage failure.
/// </para>
/// <para>
/// <b>What it does not own.</b> Whether storage "is unavailable" for the app,
/// how and when a refusal is reported to the user, and that report's
/// dismissal belong to the host. A refused result is the host's evidence; this
/// type keeps no record of it.
/// </para>
/// <para>
/// <b>Registration.</b> A host calls
/// <see cref="BrowserStorageServiceCollectionExtensions.AddBrowserStorage"/>,
/// which registers it scoped, beside the <see cref="IJSRuntime"/> it calls.
/// </para>
/// </remarks>
public sealed class BrowserStorage
{
    private readonly IJSRuntime _js;

    /// <summary>
    /// Creates an access over <paramref name="js"/>. A host obtains one by
    /// registering it with
    /// <see cref="BrowserStorageServiceCollectionExtensions.AddBrowserStorage"/>;
    /// the constructor is public for that registration, and for a test that
    /// supplies its own runtime.
    /// </summary>
    /// <param name="js">The JavaScript runtime of the scope the calls run in.</param>
    /// <exception cref="ArgumentNullException"><paramref name="js"/> is <see langword="null"/>.</exception>
    public BrowserStorage(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);
        _js = js;
    }

    /// <summary>
    /// Reads the item stored under <paramref name="key"/> in
    /// <paramref name="area"/> (<c>getItem</c>).
    /// </summary>
    /// <param name="area">The storage area to read.</param>
    /// <param name="key">The item's key. Any string, the empty string included.</param>
    /// <returns>
    /// The stored value, the absence of one, or the browser's refusal. Never
    /// faulted by a refusal.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="area"/> is not a declared <see cref="BrowserStorageArea"/>.
    /// Thrown before any call is made.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> is <see langword="null"/>. Thrown before any
    /// call is made.
    /// </exception>
    public Task<BrowserStorageReadResult> ReadAsync(BrowserStorageArea area, string key)
    {
        var identifier = Identifier(area, "getItem");
        ArgumentNullException.ThrowIfNull(key);

        return ReadCoreAsync(identifier, key);
    }

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/> in
    /// <paramref name="area"/>, replacing any item there (<c>setItem</c>).
    /// </summary>
    /// <param name="area">The storage area to write.</param>
    /// <param name="key">The item's key. Any string, the empty string included.</param>
    /// <param name="value">
    /// The value to store. Any string, the empty string included; to store
    /// nothing, call <see cref="RemoveAsync"/>.
    /// </param>
    /// <returns>Success, or the browser's refusal. Never faulted by a refusal.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="area"/> is not a declared <see cref="BrowserStorageArea"/>.
    /// Thrown before any call is made.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="value"/> is
    /// <see langword="null"/>. Thrown before any call is made.
    /// </exception>
    public Task<BrowserStorageWriteResult> WriteAsync(BrowserStorageArea area, string key, string value)
    {
        var identifier = Identifier(area, "setItem");
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        return WriteCoreAsync(identifier, key, value);
    }

    /// <summary>
    /// Removes the item stored under <paramref name="key"/> in
    /// <paramref name="area"/> (<c>removeItem</c>). Removing a key that holds
    /// no item succeeds.
    /// </summary>
    /// <param name="area">The storage area to write.</param>
    /// <param name="key">The item's key. Any string, the empty string included.</param>
    /// <returns>Success, or the browser's refusal. Never faulted by a refusal.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="area"/> is not a declared <see cref="BrowserStorageArea"/>.
    /// Thrown before any call is made.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> is <see langword="null"/>. Thrown before any
    /// call is made.
    /// </exception>
    public Task<BrowserStorageWriteResult> RemoveAsync(BrowserStorageArea area, string key)
    {
        var identifier = Identifier(area, "removeItem");
        ArgumentNullException.ThrowIfNull(key);

        return WriteCoreAsync(identifier, key);
    }

    // The argument checks above run synchronously, before the first await, so
    // a usage error throws at the call rather than faulting the task (the
    // task-based asynchronous pattern's rule for usage errors). These cores
    // hold the one interop call each, and the catch covers that call alone.
    private async Task<BrowserStorageReadResult> ReadCoreAsync(string identifier, string key)
    {
        string? stored;
        try
        {
            stored = await _js.InvokeAsync<string?>(identifier, key);
        }
        catch (JSException refusal)
        {
            return BrowserStorageReadResult.Refused(refusal);
        }

        // getItem answers null for a key with no item and the item's string
        // otherwise, the empty string included; null is the only absence.
        return stored is null
            ? BrowserStorageReadResult.Absent
            : BrowserStorageReadResult.Stored(stored);
    }

    private async Task<BrowserStorageWriteResult> WriteCoreAsync(string identifier, params object?[] args)
    {
        try
        {
            await _js.InvokeVoidAsync(identifier, args);
        }
        catch (JSException refusal)
        {
            return BrowserStorageWriteResult.Refused(refusal);
        }

        return BrowserStorageWriteResult.Success;
    }

    // The one place an area becomes a browser object. Interop resolves the
    // dotted identifier from the global scope and calls the method on that
    // object, so a browser that blocks the object itself refuses here too.
    private static string Identifier(BrowserStorageArea area, string method) => area switch
    {
        BrowserStorageArea.Local => "localStorage." + method,
        BrowserStorageArea.Session => "sessionStorage." + method,
        _ => throw new ArgumentOutOfRangeException(
            nameof(area), area, $"{nameof(BrowserStorageArea)} has no storage object for the value {area}."),
    };
}
