namespace BgUiPrimitives_Razor.TestSupport;

/// <summary>
/// Thrown when the browser storage calls a test made are not what its
/// <see cref="BrowserStoragePlan"/> said they would be: a call the plan did
/// not expect, which fails at the call; and, from
/// <see cref="BrowserStoragePlan.Verify"/>, every unexpected call, every
/// expected call not made, and every required order not kept. Also thrown by
/// a release of a held call that was never reached.
/// </summary>
/// <remarks>
/// This assembly's own type rather than a test framework's assertion
/// exception, so that it references no test framework: every framework
/// reports an exception thrown by a test as that test's failure, with this
/// message. It is not a <see cref="Microsoft.JSInterop.JSException"/>, so
/// <see cref="BrowserStorage"/> never mistakes it for the browser's refusal:
/// it propagates from the storage call that met it. Only this assembly raises
/// it.
/// </remarks>
public sealed class BrowserStorageAssertionException : Exception
{
    internal BrowserStorageAssertionException(string message)
        : base(message)
    {
    }
}
