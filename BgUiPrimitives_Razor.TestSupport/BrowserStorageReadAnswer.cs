using Microsoft.JSInterop;

namespace BgUiPrimitives_Razor.TestSupport;

/// <summary>
/// How the browser answers a planned read, in <see cref="BrowserStorage"/>'s
/// own vocabulary: with a stored value, with nothing stored, or with a
/// refusal. The real <see cref="BrowserStorage"/> turns the answer into its
/// <see cref="BrowserStorageReadResult"/>.
/// </summary>
/// <remarks>
/// Immutable, and obtained only from its three members, so no answer can be
/// something the browser would not say. Equality is reference equality: an
/// answer is declared to a plan, never compared.
/// </remarks>
public sealed class BrowserStorageReadAnswer
{
    private readonly Kind _kind;
    private readonly string? _value;

    private BrowserStorageReadAnswer(Kind kind, string? value)
    {
        _kind = kind;
        _value = value;
    }

    private enum Kind { Stored, Absent, Refused }

    /// <summary>
    /// The browser holds no item under the key. The read is
    /// <see cref="BrowserStorageReadResult.IsAbsent"/>.
    /// </summary>
    public static BrowserStorageReadAnswer Absent { get; } = new(Kind.Absent, value: null);

    /// <summary>
    /// The browser refuses the read. The read is
    /// <see cref="BrowserStorageReadResult.IsRefused"/>, carrying a
    /// <see cref="JSException"/> raised for this one call.
    /// </summary>
    public static BrowserStorageReadAnswer Refused { get; } = new(Kind.Refused, value: null);

    /// <summary>
    /// The browser holds <paramref name="value"/> under the key. The read is
    /// <see cref="BrowserStorageReadResult.IsStored"/> with that
    /// <see cref="BrowserStorageReadResult.Value"/>.
    /// </summary>
    /// <param name="value">The stored string. Any string, the empty string included.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="value"/> is <see langword="null"/>; a key with no item
    /// is <see cref="Absent"/>.
    /// </exception>
    public static BrowserStorageReadAnswer Stored(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new(Kind.Stored, value);
    }

    /// <summary>Describes the answer, for a failure message.</summary>
    /// <returns>The description.</returns>
    public override string ToString() => _kind switch
    {
        Kind.Stored => $"stored '{_value}'",
        Kind.Absent => "absent",
        _ => "refused",
    };

    // Answers one call: getItem's string for a stored value, its null for no
    // item, and the browser's error for a refusal — what interop hands
    // BrowserStorage, for it to read as it reads the browser.
    internal void Answer(TaskCompletionSource<string?> call, string description)
    {
        switch (_kind)
        {
            case Kind.Stored:
                call.SetResult(_value);
                break;
            case Kind.Absent:
                call.SetResult(null);
                break;
            default:
                call.SetException(BrowserStoragePlan.Refusal(description));
                break;
        }
    }
}
