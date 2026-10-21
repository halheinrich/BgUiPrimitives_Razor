using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace BgUiPrimitives_Razor;

/// <summary>
/// <see cref="BrowserStorage"/>, with every refusal reported: each operation
/// makes the accessor's one call, and when the browser refuses it, reports the
/// refusal to the app's <see cref="BrowserStorageCondition"/> and logs one
/// warning in the caller's words, then returns the accessor's own result.
/// </summary>
/// <remarks>
/// <para>
/// <b>The caller says what a refusal costs.</b> Each operation takes a
/// consequence phrase — what the refusal means for this caller, such as "the
/// mix panel starts blank for this visit" — and the warning carries it with
/// the operation, the key and the browser's exception. The condition holds no
/// logger, so this is the one log line per refusal.
/// </para>
/// <para>
/// <b>Results, not values.</b> A caller that must tell a refusal from nothing
/// stored reads the result; the common caller reads
/// <c>read.IsStored ? read.Value : null</c>, the refusal already reported.
/// There is no latch, retry or default here: those stay the caller's, as
/// <see cref="BrowserStorage"/> says. A success reports nothing and logs
/// nothing.
/// </para>
/// <para>
/// <b>Recorded first, logged second, contained.</b> The occurrence is recorded
/// before the warning is written, and a logger that throws is contained around
/// the logging call alone, so a failing logging provider never costs the
/// occurrence or the result.
/// </para>
/// <para>
/// <b>The accessor's boundary, unchanged.</b> Anything
/// <see cref="BrowserStorage"/> throws or propagates — an argument error, the
/// platform refusing an interop call, a timeout — propagates from here
/// unchanged, and is reported to nothing: only the browser's refusal is a
/// refusal. Called on the renderer's synchronization context, as the
/// condition is.
/// </para>
/// <para>
/// <b>Registration.</b> A host calls
/// <see cref="BrowserStorageServiceCollectionExtensions.AddReportingBrowserStorage"/>.
/// </para>
/// </remarks>
public sealed class ReportingBrowserStorage
{
    // The one message template for every refusal, stated once.
    private static readonly Action<ILogger, string, string, string, Exception?> LogRefused =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Warning,
            new EventId(1, "BrowserStorageRefused"),
            "The browser refused a storage {Operation} of the key {Key}; {Consequence}.");

    private readonly BrowserStorage _storage;
    private readonly BrowserStorageCondition _condition;
    private readonly ILogger<ReportingBrowserStorage> _logger;

    /// <summary>
    /// Creates a reporting access over <paramref name="storage"/>, reporting
    /// to <paramref name="condition"/>. A host obtains one by registering it
    /// with
    /// <see cref="BrowserStorageServiceCollectionExtensions.AddReportingBrowserStorage"/>;
    /// the constructor is public for that registration, and for a test that
    /// supplies its own parts.
    /// </summary>
    /// <param name="storage">The accessor each operation calls.</param>
    /// <param name="condition">The app's storage condition, told of each refusal.</param>
    /// <param name="logger">Where each refusal's warning is written.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public ReportingBrowserStorage(
        BrowserStorage storage, BrowserStorageCondition condition, ILogger<ReportingBrowserStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(logger);
        _storage = storage;
        _condition = condition;
        _logger = logger;
    }

    /// <summary>
    /// Reads the item stored under <paramref name="key"/> in
    /// <paramref name="area"/>, as <see cref="BrowserStorage.ReadAsync"/> does,
    /// reporting a refusal.
    /// </summary>
    /// <param name="area">The storage area to read.</param>
    /// <param name="key">The item's key.</param>
    /// <param name="consequence">What a refusal costs the caller, in its own words.</param>
    /// <returns>The accessor's result, unchanged; a refusal is reported before it returns.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="consequence"/> is null, empty or white space. Thrown
    /// before any call is made.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="area"/> is not a declared <see cref="BrowserStorageArea"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public Task<BrowserStorageReadResult> ReadAsync(BrowserStorageArea area, string key, string consequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consequence);

        return ReadCoreAsync(_storage.ReadAsync(area, key), key, consequence);
    }

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/> in
    /// <paramref name="area"/>, as <see cref="BrowserStorage.WriteAsync"/>
    /// does, reporting a refusal.
    /// </summary>
    /// <param name="area">The storage area to write.</param>
    /// <param name="key">The item's key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="consequence">What a refusal costs the caller, in its own words.</param>
    /// <returns>The accessor's result, unchanged; a refusal is reported before it returns.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="consequence"/> is null, empty or white space. Thrown
    /// before any call is made.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="area"/> is not a declared <see cref="BrowserStorageArea"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public Task<BrowserStorageWriteResult> WriteAsync(
        BrowserStorageArea area, string key, string value, string consequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consequence);

        return WriteCoreAsync(_storage.WriteAsync(area, key, value), "write", key, consequence);
    }

    /// <summary>
    /// Removes the item stored under <paramref name="key"/> in
    /// <paramref name="area"/>, as <see cref="BrowserStorage.RemoveAsync"/>
    /// does, reporting a refusal.
    /// </summary>
    /// <param name="area">The storage area to write.</param>
    /// <param name="key">The item's key.</param>
    /// <param name="consequence">What a refusal costs the caller, in its own words.</param>
    /// <returns>The accessor's result, unchanged; a refusal is reported before it returns.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="consequence"/> is null, empty or white space. Thrown
    /// before any call is made.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="area"/> is not a declared <see cref="BrowserStorageArea"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public Task<BrowserStorageWriteResult> RemoveAsync(BrowserStorageArea area, string key, string consequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consequence);

        return WriteCoreAsync(_storage.RemoveAsync(area, key), "removal", key, consequence);
    }

    // The accessor's own argument checks run synchronously inside the call
    // above, so a usage error throws from here at the call too; only the
    // awaiting is deferred to these cores.
    private async Task<BrowserStorageReadResult> ReadCoreAsync(
        Task<BrowserStorageReadResult> call, string key, string consequence)
    {
        var read = await call;
        if (read.IsRefused)
            Report(read.Refusal, "read", key, consequence);
        return read;
    }

    private async Task<BrowserStorageWriteResult> WriteCoreAsync(
        Task<BrowserStorageWriteResult> call, string operation, string key, string consequence)
    {
        var written = await call;
        if (written.IsRefused)
            Report(written.Refusal, operation, key, consequence);
        return written;
    }

    private void Report(JSException refusal, string operation, string key, string consequence)
    {
        _condition.ReportRefused(refusal);

        try
        {
            LogRefused(_logger, operation, key, consequence, refusal);
        }
        catch (Exception)
        {
            // The occurrence is recorded and the result still returns; a
            // failure to log the refusal has nowhere else to go.
        }
    }
}
