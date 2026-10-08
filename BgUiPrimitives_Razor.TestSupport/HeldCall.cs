namespace BgUiPrimitives_Razor.TestSupport;

/// <summary>
/// The pending half of a held expectation: reached when the planned call
/// arrives, and completed only when the test releases it. The one place the
/// release rules live, for reads and writes alike.
/// </summary>
/// <remarks>
/// <para>
/// <b>No timeout.</b> bUnit fails a planned call it was never told how to
/// answer after <c>BunitContext.DefaultWaitTimeout</c>. A held call is
/// pending on purpose, for as long as the test says, so a timer here would
/// make the test's outcome depend on how fast it ran. An unreleased call
/// never completes.
/// </para>
/// <para>
/// <b>Continuations run asynchronously.</b> Releasing completes the call; the
/// code awaiting it resumes on its own context afterwards, never inside the
/// test's call to release.
/// </para>
/// </remarks>
internal sealed class HeldCall<TResult>
{
    private readonly Lock _gate = new();
    private TaskCompletionSource<TResult>? _pending;
    private bool _released;

    public bool IsReached
    {
        get
        {
            lock (_gate)
            {
                return _pending is not null;
            }
        }
    }

    // The plan matches at most one call to a held expectation, so this runs
    // at most once.
    public Task<TResult> Reach()
    {
        lock (_gate)
        {
            _pending = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _pending.Task;
        }
    }

    // The pending call, handed over for completion exactly once.
    public TaskCompletionSource<TResult> Release(string description)
    {
        lock (_gate)
        {
            if (_pending is null)
            {
                throw new BrowserStorageAssertionException(
                    $"{description} has not been reached: the call was never made, so there is "
                    + "nothing to release.");
            }

            if (_released)
            {
                throw new InvalidOperationException($"{description} has already been released.");
            }

            _released = true;
            return _pending;
        }
    }
}
