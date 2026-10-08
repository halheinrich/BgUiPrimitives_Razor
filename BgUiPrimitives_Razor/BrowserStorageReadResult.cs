using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace BgUiPrimitives_Razor;

/// <summary>
/// How the browser answered a <see cref="BrowserStorage.ReadAsync"/>: with a
/// stored value, with nothing stored, or with a refusal. Exactly one of
/// <see cref="IsStored"/>, <see cref="IsAbsent"/> and <see cref="IsRefused"/>
/// is <see langword="true"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stored is not valid.</b> A stored value is whatever string the browser
/// holds under the key — possibly empty, possibly written by an older build
/// or by hand. Whether it means anything is the caller's to judge, with its
/// own parser and its own default; a parse failure there is the caller's,
/// never a storage outcome.
/// </para>
/// <para>
/// <b>The empty string is stored.</b> An item whose value is
/// <c>""</c> reads as <see cref="IsStored"/> with an empty
/// <see cref="Value"/>; only a key with no item reads as
/// <see cref="IsAbsent"/>.
/// </para>
/// <para>
/// Only <see cref="BrowserStorage"/> produces one, and it is immutable. Its
/// equality is reference equality, deliberately: an outcome is read through
/// its flags and <see cref="Value"/>, and a refusal carries the browser's own
/// exception, which has no value equality to lend it.
/// </para>
/// </remarks>
public sealed class BrowserStorageReadResult
{
    // Nothing stored carries no detail, so every absent read is this instance.
    internal static readonly BrowserStorageReadResult Absent = new(value: null, refusal: null);

    private BrowserStorageReadResult(string? value, JSException? refusal)
    {
        Value = value;
        Refusal = refusal;
    }

    /// <summary>
    /// <see langword="true"/> when the browser holds an item under the key;
    /// <see cref="Value"/> is then that item's string, which may be empty.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsStored => Value is not null;

    /// <summary>
    /// <see langword="true"/> when the browser answered and holds no item
    /// under the key.
    /// </summary>
    public bool IsAbsent => Value is null && Refusal is null;

    /// <summary>
    /// <see langword="true"/> when the browser refused the read;
    /// <see cref="Refusal"/> is then the exception it raised. Nothing is known
    /// about what is stored.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Refusal))]
    public bool IsRefused => Refusal is not null;

    /// <summary>
    /// The stored string when <see cref="IsStored"/>; otherwise
    /// <see langword="null"/>.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// The browser's refusal, exactly as interop raised it, when
    /// <see cref="IsRefused"/>; otherwise <see langword="null"/>.
    /// </summary>
    public JSException? Refusal { get; }

    internal static BrowserStorageReadResult Stored(string value) => new(value, refusal: null);

    internal static BrowserStorageReadResult Refused(JSException refusal) => new(value: null, refusal);
}
