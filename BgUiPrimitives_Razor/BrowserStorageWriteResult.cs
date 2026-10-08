using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace BgUiPrimitives_Razor;

/// <summary>
/// How the browser answered a write — a <see cref="BrowserStorage.WriteAsync"/>
/// or a <see cref="BrowserStorage.RemoveAsync"/>: it succeeded, or it was
/// refused. Exactly one of <see cref="Succeeded"/> and
/// <see cref="IsRefused"/> is <see langword="true"/>.
/// </summary>
/// <remarks>
/// <para>
/// A removal is a write: it changes what is stored. Removing a key that holds
/// no item succeeds, as the browser's own <c>removeItem</c> does; there is no
/// "absent" outcome for a write, and none is found out by reading first.
/// </para>
/// <para>
/// Only <see cref="BrowserStorage"/> produces one, and it is immutable. It has
/// reference equality, for the reason <see cref="BrowserStorageReadResult"/>
/// gives.
/// </para>
/// </remarks>
public sealed class BrowserStorageWriteResult
{
    // Success carries no detail, so every successful write is this instance.
    internal static readonly BrowserStorageWriteResult Success = new(refusal: null);

    private BrowserStorageWriteResult(JSException? refusal) => Refusal = refusal;

    /// <summary>
    /// <see langword="true"/> when the browser accepted the write: the item
    /// now holds the value written, or, for a removal, the key holds no item.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Refusal))]
    public bool Succeeded => Refusal is null;

    /// <summary>
    /// <see langword="true"/> when the browser refused the write;
    /// <see cref="Refusal"/> is then the exception it raised.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Refusal))]
    public bool IsRefused => Refusal is not null;

    /// <summary>
    /// The browser's refusal, exactly as interop raised it, when
    /// <see cref="IsRefused"/>; otherwise <see langword="null"/>.
    /// </summary>
    public JSException? Refusal { get; }

    internal static BrowserStorageWriteResult Refused(JSException refusal) => new(refusal);
}
