using Microsoft.JSInterop.Infrastructure;

namespace BgUiPrimitives_Razor.TestSupport;

/// <summary>
/// A write or a removal a <see cref="BrowserStoragePlan"/> expects once and
/// holds: when the code under test makes it, the call stays pending until the
/// test releases it with the browser's answer. From
/// <see cref="BrowserStoragePlan.ExpectHeldWrite"/> and
/// <see cref="BrowserStoragePlan.ExpectHeldRemove"/>.
/// </summary>
/// <remarks>
/// For a test of what happens while a save is outstanding — a suspended save
/// across a change, or an older save completing after a newer one. Each held
/// call is its own: two held writes are matched by their values, two held
/// writes of one value or two held removals of one key in the order they
/// were declared, and each is released on its own. A held call is never
/// released by the plan, and never times out.
/// </remarks>
public sealed class BrowserStorageHeldWrite : BrowserStorageExpectation
{
    private readonly HeldCall<IJSVoidResult> _held = new();

    internal BrowserStorageHeldWrite(BrowserStoragePlan plan, StorageCall call)
        : base(plan, call)
    {
    }

    /// <summary>
    /// Whether the code under test has made the call. Once it has, the call
    /// is pending until <see cref="Release"/>.
    /// </summary>
    public bool IsReached => _held.IsReached;

    /// <summary>
    /// Completes the pending call with the browser's
    /// <paramref name="answer"/>. The code awaiting the call resumes
    /// afterwards, on its own context: await the operation, or wait for the
    /// render that follows it.
    /// </summary>
    /// <param name="answer">How the browser answers the write or removal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is <see langword="null"/>.</exception>
    /// <exception cref="BrowserStorageAssertionException">
    /// The call has not been reached: the code under test has not made it.
    /// </exception>
    /// <exception cref="InvalidOperationException">The call has already been released.</exception>
    public void Release(BrowserStorageWriteAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        answer.Answer(_held.Release(ToString()), Call.ToString());
    }

    /// <summary>Describes the expectation, for a failure message.</summary>
    /// <returns>The description.</returns>
    public override string ToString() => Call.Held;

    internal override Task<IJSVoidResult> AnswerWrite() => _held.Reach();
}
