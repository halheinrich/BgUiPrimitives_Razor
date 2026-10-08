using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace BgUiPrimitives_Razor.TestSupport;

/// <summary>
/// How the browser answers a planned write or removal, in
/// <see cref="BrowserStorage"/>'s own vocabulary: it succeeds, or it is
/// refused. The real <see cref="BrowserStorage"/> turns the answer into its
/// <see cref="BrowserStorageWriteResult"/>.
/// </summary>
/// <remarks>
/// A class with two instances rather than an enum, so that no value outside
/// the two can be written; and, like <see cref="BrowserStorageReadAnswer"/>,
/// immutable with reference equality.
/// </remarks>
public sealed class BrowserStorageWriteAnswer
{
    private readonly bool _refused;

    private BrowserStorageWriteAnswer(bool refused) => _refused = refused;

    /// <summary>
    /// The browser accepts the write. It is
    /// <see cref="BrowserStorageWriteResult.Succeeded"/>.
    /// </summary>
    public static BrowserStorageWriteAnswer Succeeded { get; } = new(refused: false);

    /// <summary>
    /// The browser refuses the write. It is
    /// <see cref="BrowserStorageWriteResult.IsRefused"/>, carrying a
    /// <see cref="JSException"/> raised for this one call.
    /// </summary>
    public static BrowserStorageWriteAnswer Refused { get; } = new(refused: true);

    /// <summary>Describes the answer, for a failure message.</summary>
    /// <returns>The description.</returns>
    public override string ToString() => _refused ? "refused" : "succeeded";

    // Answers one call: setItem and removeItem return nothing when they
    // succeed, and raise the browser's error when they are refused.
    internal void Answer(TaskCompletionSource<IJSVoidResult> call, string description)
    {
        if (_refused)
        {
            call.SetException(BrowserStoragePlan.Refusal(description));
        }
        else
        {
            call.SetResult(null!);
        }
    }
}
