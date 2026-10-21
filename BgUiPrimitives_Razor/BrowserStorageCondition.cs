using Microsoft.JSInterop;

namespace BgUiPrimitives_Razor;

/// <summary>
/// The app's one storage condition: whether the browser has refused a storage
/// call this visit, and whether the user has dismissed the notice that says
/// so. One per loaded app; every refusal, from any caller, reports here, so
/// the user is told one thing however many keys were refused.
/// </summary>
/// <remarks>
/// <para>
/// <b>One occurrence per visit, and a dismissal that holds</b> (the umbrella's
/// <c>SPEC-notices.md</c> §2). The first refusal reported begins the
/// occurrence; every later one is the same occurrence, so a dismissal survives
/// navigation and further refusals. A success ends nothing and begins nothing.
/// A reload constructs a fresh holder: a fresh visit, with no occurrence.
/// Neither bit is ever stored.
/// </para>
/// <para>
/// <b>It records; it never gates.</b> Nothing consults it before a storage
/// call, and nothing here can stop one: like <see cref="BrowserStorage"/>, it
/// is no latch.
/// </para>
/// <para>
/// <b>No logger.</b> The condition is one fact however it arose. What a
/// refusal costs is the caller's to say, and
/// <see cref="ReportingBrowserStorage"/> logs it once per refusal, in the
/// caller's words.
/// </para>
/// <para>
/// <b>Observed, not polled.</b> A refusal can arrive while no gesture of the
/// observing page is running, so a page attaches (<see cref="Attach"/>) and
/// re-renders off the notification. Every member is called on the renderer's
/// synchronization context; the type is not thread-safe.
/// </para>
/// <para>
/// <b>Registration.</b> A host calls
/// <see cref="BrowserStorageServiceCollectionExtensions.AddReportingBrowserStorage"/>.
/// </para>
/// </remarks>
public sealed class BrowserStorageCondition
{
    private readonly List<Action> _observers = [];

    /// <summary>
    /// Whether the browser has refused a storage call this visit — the
    /// occurrence. Once <see langword="true"/>, it stays so until a reload.
    /// </summary>
    public bool HasOccurred { get; private set; }

    /// <summary>
    /// Whether the user has dismissed the notice reporting
    /// <see cref="HasOccurred"/>. Only ever <see langword="true"/> while
    /// <see cref="HasOccurred"/> is.
    /// </summary>
    public bool IsNoticeDismissed { get; private set; }

    /// <summary>
    /// Reports that the browser refused a storage call. The first begins the
    /// occurrence and notifies the observers; every later one is the same
    /// occurrence and changes nothing.
    /// </summary>
    /// <param name="refusal">The browser's exception, as the accessor returned it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="refusal"/> is <see langword="null"/>.</exception>
    public void ReportRefused(JSException refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);

        if (HasOccurred) return;

        HasOccurred = true;
        Publish();
    }

    /// <summary>
    /// Records that the user dismissed the notice, and notifies the observers.
    /// Dismissing again changes nothing.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Nothing has been refused, so no such notice can be on screen. A
    /// dismissal recorded then would hide the occurrence before it began.
    /// </exception>
    public void DismissNotice()
    {
        if (!HasOccurred)
            throw new InvalidOperationException(
                "No storage call has been refused, so there is no notice to dismiss.");
        if (IsNoticeDismissed) return;

        IsNoticeDismissed = true;
        Publish();
    }

    /// <summary>
    /// Observes the condition: <paramref name="observer"/> is called once
    /// before this returns, for the current state, and then after each change,
    /// until the returned handle is disposed (disposing twice is harmless). An
    /// observer reads <see cref="HasOccurred"/> and
    /// <see cref="IsNoticeDismissed"/> and schedules its own render; it must
    /// not throw.
    /// </summary>
    /// <param name="observer">Called with no arguments on each notification.</param>
    /// <returns>The attachment; disposing it stops the observation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="observer"/> is <see langword="null"/>.</exception>
    public IDisposable Attach(Action observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        _observers.Add(observer);
        observer();
        return new Attachment(this, observer);
    }

    // A copy, so an observer that detaches while being told changes nothing
    // about who else is told this time.
    private void Publish()
    {
        foreach (var observer in _observers.ToArray())
            observer();
    }

    private sealed class Attachment(BrowserStorageCondition owner, Action observer) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner._observers.Remove(observer);
        }
    }
}
