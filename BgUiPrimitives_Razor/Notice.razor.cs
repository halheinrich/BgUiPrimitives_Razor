using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BgUiPrimitives_Razor;

/// <summary>
/// A notice box: the one owner of alert markup, so that no member types it
/// again. The caller says what the notice <em>is</em> — its
/// <see cref="Kind"/>, its <see cref="Urgency"/>, whether it is
/// <see cref="Dismissible"/> — and the component owns what that means in
/// classes, roles and gestures. The model it implements is the umbrella's
/// SPEC-notices.md, sections 2 and 3.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dismissal.</b> A dismissal belongs to one occurrence of one notice,
/// lives exactly as long as that occurrence, and is never stored. The owner
/// of the occurrence holds it, and there is exactly one holder per notice:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>The component holds it</b> when the occurrence dies with the page. The
/// caller writes nothing. If a new occurrence can replace the old one while
/// the notice stays rendered, the caller names it with
/// <see cref="OccurrenceKey"/> and the new occurrence shows fresh.
/// </description></item>
/// <item><description>
/// <b>The caller holds it</b> when the occurrence outlives the component — an
/// app-scoped holder that navigation must not reset. The caller binds
/// <see cref="Dismissed"/> (<c>@bind-Dismissed</c>, or its <c>:get</c> /
/// <c>:set</c> form over a holder's methods). The component then keeps no
/// copy: it renders from the bound value and reports the gesture, and the
/// owner's value always wins.
/// </description></item>
/// </list>
/// <para>
/// A non-dismissible notice always renders, whatever dismissed state is
/// supplied: a gate reason or live indicator must never be hidden by a
/// dismissal.
/// </para>
/// <para>
/// <b>Gestures.</b> A dismissible notice offers both a visible close button,
/// which carries the keyboard and screen-reader semantics, and the whole box
/// as the large click target. Interactive content inside the notice (links,
/// buttons, form controls) does its own job and never dismisses; the caller
/// adds nothing to get that.
/// </para>
/// <para>
/// <b>Host obligations.</b> The component emits Bootstrap 5 alert classes
/// (<c>alert</c>, <c>alert-*</c>, <c>alert-dismissible</c>,
/// <c>btn-close</c>); the host supplies Bootstrap. The little Bootstrap lacks
/// ships as this library's scoped CSS, which reaches a host through its
/// <c>{HostAssembly}.styles.css</c> bundle; the host must link that bundle.
/// </para>
/// </remarks>
public sealed partial class Notice : ComponentBase
{
    // The close button's accessible name: one owner, so every notice in every
    // member reads the same to a screen reader.
    private const string CloseButtonLabel = "Dismiss this message";

    // Attribute names the component owns on the box. A caller's copy would
    // lose to the component's silently, so it is refused instead.
    private static readonly string[] OwnedAttributeNames = ["role", "onclick"];

    // True when the caller bound Dismissed, and so holds the dismissal. Decided
    // by which parameters were supplied, never by their values.
    private bool _callerHoldsDismissal;

    // The component-held dismissal and the occurrence it belongs to. Read and
    // written only while the caller does not hold the dismissal, so there is
    // never a second copy of the bit that could disagree with the first.
    private bool _heldDismissed;
    private object? _heldOccurrenceKey;

    /// <summary>
    /// What the notice is telling the user; decides the box's colour and
    /// nothing else. Required.
    /// </summary>
    [Parameter, EditorRequired]
    public NoticeKind Kind { get; set; }

    /// <summary>The notice's content. Required.</summary>
    [Parameter, EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// How insistently the notice is announced. Defaults to
    /// <see cref="NoticeUrgency.Polite"/>; choose
    /// <see cref="NoticeUrgency.Assertive"/> for a failed gesture or an
    /// impending loss. Independent of <see cref="Kind"/>.
    /// </summary>
    [Parameter]
    public NoticeUrgency Urgency { get; set; }

    /// <summary>
    /// Whether the user may dismiss the notice. Defaults to
    /// <see langword="false"/>: an accidentally dismissible gate reason leaves
    /// a screen that cannot explain itself, while an accidentally sticky event
    /// notice is only a nuisance. A notice that is not dismissible renders no
    /// close button, reacts to no click, and ignores any dismissed state.
    /// </summary>
    [Parameter]
    public bool Dismissible { get; set; }

    /// <summary>
    /// The dismissed state, for a caller that holds it. Supply it only as a
    /// two-way binding (<c>@bind-Dismissed</c>), paired with
    /// <see cref="DismissedChanged"/>; supplying one without the other throws.
    /// When bound, the component keeps no dismissed state of its own and the
    /// bound value always wins. Leave it unbound when the occurrence dies with
    /// the page and the component should hold the dismissal itself.
    /// </summary>
    [Parameter]
    public bool Dismissed { get; set; }

    /// <summary>
    /// Raised with <see langword="true"/> when the user dismisses a notice
    /// whose caller holds the dismissal. The binding half of
    /// <see cref="Dismissed"/>; not raised when the component holds it.
    /// </summary>
    [Parameter]
    public EventCallback<bool> DismissedChanged { get; set; }

    /// <summary>
    /// Names the current occurrence for a component-held dismissal. When the
    /// value changes (compared with <see cref="object.Equals(object, object)"/>)
    /// the component forgets its dismissal and the notice shows fresh. Needed
    /// only where a new occurrence can arrive while the notice stays rendered;
    /// a notice that is removed and rendered again starts fresh regardless.
    /// Supplying it together with a bound <see cref="Dismissed"/> throws: a
    /// caller that holds the dismissal also holds its occurrences.
    /// </summary>
    [Parameter]
    public object? OccurrenceKey { get; set; }

    /// <summary>
    /// Every other attribute — <c>id</c>, <c>style</c>, <c>data-*</c>,
    /// <c>aria-*</c> — lands on the box element itself. A <c>class</c> is
    /// merged after the component's own classes, never replacing them.
    /// <c>role</c> and <c>onclick</c> belong to the component
    /// (<see cref="Urgency"/>, <see cref="Dismissible"/>) and throw if
    /// supplied.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsDismissed =>
        Dismissible && (_callerHoldsDismissal ? Dismissed : _heldDismissed);

    private string BoxClass
    {
        get
        {
            var own = Dismissible
                ? $"alert {KindClass} alert-dismissible"
                : $"alert {KindClass}";

            return AdditionalAttributes is not null
                && AdditionalAttributes.TryGetValue("class", out var callerClass)
                && Convert.ToString(callerClass) is { } extra
                && !string.IsNullOrWhiteSpace(extra)
                    ? $"{own} {extra.Trim()}"
                    : own;
        }
    }

    private string KindClass => Kind switch
    {
        NoticeKind.Information => "alert-info",
        NoticeKind.Warning => "alert-warning",
        NoticeKind.Error => "alert-danger",
        NoticeKind.Success => "alert-success",
        _ => throw new InvalidOperationException(
            $"{nameof(Notice)}.{nameof(Kind)} has no stylesheet class for the value {Kind}."),
    };

    private string Role => Urgency switch
    {
        NoticeUrgency.Polite => "status",
        NoticeUrgency.Assertive => "alert",
        _ => throw new InvalidOperationException(
            $"{nameof(Notice)}.{nameof(Urgency)} has no ARIA role for the value {Urgency}."),
    };

    // No delegate when the notice is not dismissible, so the renderer emits no
    // click handler for the box at all (see Notice.razor).
    private EventCallback<MouseEventArgs> BoxClick => Dismissible
        ? EventCallback.Factory.Create<MouseEventArgs>(this, DismissAsync)
        : default;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // Who holds the dismissal is a property of the call site — which
        // parameters it supplies — so it is read from the ParameterView, the
        // only place presence (rather than value) is visible.
        var hasDismissed = parameters.TryGetValue<bool>(nameof(Dismissed), out _);
        var hasDismissedChanged =
            parameters.TryGetValue<EventCallback<bool>>(nameof(DismissedChanged), out _);

        if (hasDismissed != hasDismissedChanged)
        {
            // Dismissed alone is a value the user's gesture cannot change;
            // DismissedChanged alone is a gesture that hides nothing. Either
            // is a notice whose screen and owner disagree.
            throw new InvalidOperationException(
                $"{nameof(Notice)}: {nameof(Dismissed)} and {nameof(DismissedChanged)} must be "
                + $"supplied together, as a two-way binding (@bind-{nameof(Dismissed)}).");
        }

        if (hasDismissedChanged && parameters.TryGetValue<object?>(nameof(OccurrenceKey), out _))
        {
            throw new InvalidOperationException(
                $"{nameof(Notice)}: {nameof(OccurrenceKey)} resets only a dismissal the component "
                + $"holds. A caller that binds {nameof(Dismissed)} holds the dismissal and its "
                + "occurrences; remove one of the two.");
        }

        _callerHoldsDismissal = hasDismissedChanged;

        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (AdditionalAttributes is not null)
        {
            foreach (var name in AdditionalAttributes.Keys)
            {
                if (OwnedAttributeNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"{nameof(Notice)} owns the box's '{name}' attribute; use "
                        + $"{nameof(Urgency)} for the role and {nameof(Dismissible)} for the "
                        + "click. An attribute passed here would be silently overridden.");
                }
            }
        }

        if (!Equals(OccurrenceKey, _heldOccurrenceKey))
        {
            _heldOccurrenceKey = OccurrenceKey;
            _heldDismissed = false;
        }
    }

    private Task DismissAsync()
    {
        if (_callerHoldsDismissal)
        {
            return DismissedChanged.InvokeAsync(true);
        }

        _heldDismissed = true;
        return Task.CompletedTask;
    }
}
