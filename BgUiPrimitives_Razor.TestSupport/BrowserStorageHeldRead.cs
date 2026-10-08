namespace BgUiPrimitives_Razor.TestSupport;

/// <summary>
/// A read a <see cref="BrowserStoragePlan"/> expects once and holds: when the
/// code under test makes it, the read stays pending until the test releases
/// it with the browser's answer. From
/// <see cref="BrowserStoragePlan.ExpectHeldRead"/>.
/// </summary>
/// <remarks>
/// For a test of what happens while a read is outstanding — a restoration
/// pending across a change, or an older read completing after a newer one.
/// Each held read is its own call: two held reads of one key are matched to
/// the calls in the order they were declared, and each is released on its
/// own. A held read is never released by the plan, and never times out.
/// </remarks>
public sealed class BrowserStorageHeldRead : BrowserStorageExpectation
{
    private readonly HeldCall<string?> _held = new();

    internal BrowserStorageHeldRead(BrowserStoragePlan plan, StorageCall call)
        : base(plan, call)
    {
    }

    /// <summary>
    /// Whether the code under test has made the read. Once it has, the read
    /// is pending until <see cref="Release"/>.
    /// </summary>
    public bool IsReached => _held.IsReached;

    /// <summary>
    /// Completes the pending read with the browser's
    /// <paramref name="answer"/>. The code awaiting the read resumes
    /// afterwards, on its own context: await the operation, or wait for the
    /// render that follows it.
    /// </summary>
    /// <param name="answer">How the browser answers the read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is <see langword="null"/>.</exception>
    /// <exception cref="BrowserStorageAssertionException">
    /// The read has not been reached: the code under test has not made it.
    /// </exception>
    /// <exception cref="InvalidOperationException">The read has already been released.</exception>
    public void Release(BrowserStorageReadAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        answer.Answer(_held.Release(ToString()), Call.ToString());
    }

    /// <summary>Describes the expectation, for a failure message.</summary>
    /// <returns>The description.</returns>
    public override string ToString() => Call.Held;

    internal override Task<string?> AnswerRead() => _held.Reach();
}
