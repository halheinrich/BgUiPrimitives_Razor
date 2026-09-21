using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace BgUiPrimitives_Razor.TestSupport;

/// <summary>
/// One rendered <see cref="Notice"/>, read back into the component's own
/// vocabulary: its <see cref="Kind"/>, its <see cref="Announcement"/>, whether
/// it is <see cref="Dismissible"/>, and what the call site put on it. The one
/// place outside the component that knows what a notice looks like in markup,
/// so that no consumer test names a class, a role or a selector of the
/// component's.
/// </summary>
/// <remarks>
/// <para>
/// <b>An instance is a well-formed notice.</b> Every way of obtaining one
/// reads the whole box and throws <see cref="NoticeAssertionException"/>
/// unless it is what <see cref="Notice"/> renders: the box and its content
/// wrapper; each of the component's classes exactly once; an announcement that
/// lives on the content and nowhere else, with no live-region attribute
/// anywhere in the box; and a dismissibility the box agrees with itself about
/// — the close button (outside the announced content) and the click handler
/// together, or neither, and never a click handler on the content or on the
/// close button.
/// <see cref="ShouldBe"/> then compares what was read with what the test says
/// the call site asked for.
/// </para>
/// <para>
/// <b>An instance is a reading of one render.</b> It does not follow the
/// component through a re-render; after <see cref="Dismiss"/>, or anything
/// else that renders again, find the notice again.
/// </para>
/// <para>
/// <b>An independent reader, by design.</b> What the component renders is
/// restated here rather than shared with it through internals: a reader that
/// took its expectations from the implementation would agree with whatever the
/// implementation did. This repository's tests hold the two together — they
/// render the real component through this type, and hand it markup that breaks
/// each rule.
/// </para>
/// </remarks>
public sealed class NoticeBox
{
    private const string BoxClass = "alert";
    private const string DismissibleClass = "alert-dismissible";
    private const string ContentClass = "bg-notice-content";
    private const string CloseButtonClass = "btn-close";

    private const string ClassAttribute = "class";
    private const string RoleAttribute = "role";

    // What the renderer and the component stamp on the box, beyond its class:
    // the scope attribute of CSS isolation (b-…), and the test renderer's
    // marker for a bound event handler (blazor:on…). The click marker on the
    // box is the component's own dismiss handler; any other marker is a
    // handler the call site bound, reported under the name it was written as.
    private const string ScopeAttributePrefix = "b-";
    private const string EventMarkerPrefix = "blazor:";
    private const string ClickMarker = "blazor:onclick";

    private static readonly Dictionary<string, NoticeKind> KindsByClass = new(StringComparer.Ordinal)
    {
        ["alert-info"] = NoticeKind.Information,
        ["alert-warning"] = NoticeKind.Warning,
        ["alert-danger"] = NoticeKind.Error,
        ["alert-success"] = NoticeKind.Success,
    };

    private static readonly string[] LiveRegionAttributes = ["aria-live", "aria-atomic", "aria-relevant"];

    // WAI-ARIA 1.2, section 5.3.5: the live-region roles.
    private static readonly string[] LiveRegionRoles = ["alert", "log", "marquee", "status", "timer"];

    private readonly IElement? _closeButton;

    private NoticeBox(
        IElement element,
        IElement content,
        IElement? closeButton,
        NoticeKind kind,
        NoticeAnnouncement announcement,
        IReadOnlyList<string> callerClasses,
        IReadOnlyList<string> callerAttributes)
    {
        Element = element;
        Content = content;
        _closeButton = closeButton;
        Kind = kind;
        Announcement = announcement;
        CallerClasses = callerClasses;
        CallerAttributes = callerAttributes;
    }

    /// <summary>
    /// The box element: where the call site's <c>id</c>, <c>style</c> and
    /// other attributes land, for a test to read its own values back.
    /// </summary>
    public IElement Element { get; }

    /// <summary>
    /// The element holding what the call site put inside the notice — and
    /// nothing of the component's — for a test to read its own content back.
    /// </summary>
    public IElement Content { get; }

    /// <summary>The kind the notice rendered as.</summary>
    public NoticeKind Kind { get; }

    /// <summary>How the notice rendered its announcement.</summary>
    public NoticeAnnouncement Announcement { get; }

    /// <summary>Whether the notice offers the user its two dismiss gestures.</summary>
    public bool Dismissible => _closeButton is not null;

    /// <summary>
    /// The classes the call site passed, in order, without the component's own.
    /// </summary>
    public IReadOnlyList<string> CallerClasses { get; }

    /// <summary>
    /// The names of the attributes the call site put on the box, in rendered
    /// order: everything on it that the component and the renderer did not
    /// stamp. <c>class</c> is among them only when the call site passed one,
    /// and an event handler the call site bound appears under the name it was
    /// written as (<c>onmouseover</c>).
    /// </summary>
    public IReadOnlyList<string> CallerAttributes { get; }

    /// <summary>
    /// Reads <paramref name="element"/> as a notice box. The way in for a test
    /// that found the element by an identifier of its own that the finders do
    /// not cover.
    /// </summary>
    /// <param name="element">The element the test believes is a notice box.</param>
    /// <returns>The notice, read.</returns>
    /// <exception cref="NoticeAssertionException">
    /// The element is not a notice box — the identifier is on something else,
    /// or on a hand-written box — or is not a well-formed one.
    /// </exception>
    public static NoticeBox From(IElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (!IsBox(element))
        {
            throw Failure(
                "This element is not a notice box: it is not what <Notice> renders. The "
                + "identifier that found it is on another element, or the box is hand-written.",
                element);
        }

        return Read(element);
    }

    /// <summary>
    /// Finds the notice the call site gave <paramref name="id"/>. The id must
    /// be on the box itself, as <see cref="Notice"/> puts it.
    /// </summary>
    /// <param name="rendered">The rendered component to look in.</param>
    /// <param name="id">The call site's <c>id</c>.</param>
    /// <returns>The notice, read.</returns>
    /// <exception cref="NoticeAssertionException">
    /// No element, or more than one, carries the id; or the one that does is
    /// not a well-formed notice box.
    /// </exception>
    public static NoticeBox ById(IRenderedComponent<IComponent> rendered, string id)
    {
        ArgumentNullException.ThrowIfNull(rendered);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var found = ElementsOf(rendered)
            .Where(e => string.Equals(e.Id, id, StringComparison.Ordinal))
            .ToList();

        return From(TheOnly(found, $"with the id '{id}'", rendered));
    }

    /// <summary>
    /// Finds the one notice the call site marked with
    /// <paramref name="markerClass"/> — a class of the caller's own, never one
    /// of the component's.
    /// </summary>
    /// <param name="rendered">The rendered component to look in.</param>
    /// <param name="markerClass">The call site's marker class, a single class name.</param>
    /// <returns>The notice, read.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="markerClass"/> is not a single class name, or is a
    /// class the component owns.
    /// </exception>
    /// <exception cref="NoticeAssertionException">
    /// No element, or more than one, carries the class; or the one that does
    /// is not a well-formed notice box.
    /// </exception>
    public static NoticeBox ByMarkerClass(IRenderedComponent<IComponent> rendered, string markerClass)
    {
        ArgumentNullException.ThrowIfNull(rendered);
        ArgumentException.ThrowIfNullOrWhiteSpace(markerClass);

        if (markerClass.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                $"'{markerClass}' is not a single class name.", nameof(markerClass));
        }

        if (IsComponentClass(markerClass))
        {
            throw new ArgumentException(
                $"'{markerClass}' is a class the Notice component owns, not a marker of the call "
                + $"site's. Say what the notice is with {nameof(ShouldBe)}, and find it by an id "
                + "or class the call site passed.",
                nameof(markerClass));
        }

        var found = ElementsOf(rendered)
            .Where(e => ClassesOf(e).Contains(markerClass, StringComparer.Ordinal))
            .ToList();

        return From(TheOnly(found, $"with the class '{markerClass}'", rendered));
    }

    /// <summary>
    /// Every notice in <paramref name="rendered"/>, in document order. Empty
    /// when none is shown — a dismissed notice renders nothing.
    /// </summary>
    /// <param name="rendered">The rendered component to look in.</param>
    /// <returns>The notices, each read.</returns>
    /// <exception cref="NoticeAssertionException">
    /// A notice box in <paramref name="rendered"/> is not well-formed.
    /// </exception>
    public static IReadOnlyList<NoticeBox> AllIn(IRenderedComponent<IComponent> rendered)
    {
        ArgumentNullException.ThrowIfNull(rendered);

        return [.. ElementsOf(rendered).Where(IsBox).Select(Read)];
    }

    /// <summary>
    /// Asserts that the notice is what the call site asked for. Everything is
    /// stated at once, and nothing is optional, because the failure this
    /// exists for is silent: a misspelt or renamed parameter on a
    /// <c>&lt;Notice&gt;</c> tag compiles clean, lands on the box as a plain
    /// attribute, and leaves the real parameter at its default — polite, not
    /// dismissible.
    /// </summary>
    /// <param name="kind">The call site's <c>Kind</c>.</param>
    /// <param name="announcement">
    /// The call site's <c>Announcement</c> (<see cref="NoticeAnnouncement.Polite"/>
    /// if it names none).
    /// </param>
    /// <param name="dismissible">
    /// The call site's <c>Dismissible</c> (<see langword="false"/> if it names none).
    /// </param>
    /// <param name="callerAttributes">
    /// The names of the attributes the call site passes that are not
    /// parameters, as written on the tag — <c>"id"</c>, <c>"class"</c>,
    /// <c>"style"</c>, <c>"data-…"</c>; none if it passes none. The box must
    /// carry exactly these.
    /// </param>
    /// <returns>This notice, to go on to <see cref="Dismiss"/> or a read.</returns>
    /// <exception cref="NoticeAssertionException">
    /// The notice differs; the message states every difference.
    /// </exception>
    public NoticeBox ShouldBe(
        NoticeKind kind,
        NoticeAnnouncement announcement,
        bool dismissible,
        params string[] callerAttributes)
    {
        ArgumentNullException.ThrowIfNull(callerAttributes);

        var differences = new List<string>();

        if (Kind != kind)
        {
            differences.Add($"its kind is {Kind}, not {kind}");
        }

        if (Announcement != announcement)
        {
            differences.Add($"its announcement is {Announcement}, not {announcement}");
        }

        if (Dismissible != dismissible)
        {
            differences.Add(Dismissible ? "it is dismissible" : "it is not dismissible");
        }

        var unnamed = CallerAttributes
            .Except(callerAttributes, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unnamed.Count > 0)
        {
            differences.Add(
                $"the box carries {Quoted(unnamed)}, which the test did not name — a misspelt or "
                + "renamed parameter lands on the box like this, and the real parameter takes "
                + "its default");
        }

        var absent = callerAttributes
            .Except(CallerAttributes, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (absent.Count > 0)
        {
            differences.Add($"the box does not carry {Quoted(absent)}, which the test named");
        }

        if (differences.Count > 0)
        {
            throw Failure(
                $"The notice is not what the test says: {string.Join("; ", differences)}.",
                Element);
        }

        return this;
    }

    /// <summary>
    /// Makes one of the user's two dismiss gestures, as a real click through
    /// the test renderer. What follows is the owner's to decide — a
    /// caller-held dismissal may decline — so nothing is asserted about it,
    /// and this reading is stale afterwards.
    /// </summary>
    /// <param name="gesture">Which of the two gestures.</param>
    /// <exception cref="NoticeAssertionException">
    /// The notice is not dismissible, and so offers neither gesture.
    /// </exception>
    public void Dismiss(NoticeDismissGesture gesture)
    {
        if (_closeButton is null)
        {
            throw Failure("The notice is not dismissible: it offers no dismiss gesture.", Element);
        }

        var target = gesture switch
        {
            NoticeDismissGesture.CloseButton => _closeButton,
            NoticeDismissGesture.WholeBox => Element,
            _ => throw new ArgumentOutOfRangeException(nameof(gesture), gesture, message: null),
        };

        target.Click();
    }

    // ---- Reading ---------------------------------------------------------

    // What makes an element a notice box at all, as opposed to a box that is
    // malformed: the alert element with the component's content wrapper as its
    // direct child. A hand-written alert has no such child.
    private static bool IsBox(IElement element) =>
        element.LocalName == "div"
        && ClassesOf(element).Contains(BoxClass, StringComparer.Ordinal)
        && ContentWrappersOf(element).Count == 1;

    private static NoticeBox Read(IElement box)
    {
        var content = ContentWrappersOf(box)[0];
        var (kind, callerClasses) = ReadClasses(box);
        var closeButton = ReadDismissibility(box, content);
        var announcement = ReadAnnouncement(box, content);

        return new NoticeBox(
            box, content, closeButton, kind, announcement, callerClasses,
            ReadCallerAttributes(box, callerClasses));
    }

    // The component's classes appear exactly once each; whatever else is in
    // the list is the call site's. A component class left among the caller's
    // is one the call site passed as well — the migration slip of keeping
    // "alert alert-danger" in the class handed to the component.
    private static (NoticeKind Kind, IReadOnlyList<string> CallerClasses) ReadClasses(IElement box)
    {
        var classes = ClassesOf(box);
        var kindClasses = classes.Where(KindsByClass.ContainsKey).Distinct(StringComparer.Ordinal).ToList();

        if (kindClasses.Count != 1)
        {
            throw Failure(
                kindClasses.Count == 0
                    ? "The notice box states no kind."
                    : $"The notice box states more than one kind ({Quoted(kindClasses)}).",
                box);
        }

        var callerClasses = classes.ToList();
        callerClasses.Remove(BoxClass);
        callerClasses.Remove(kindClasses[0]);
        callerClasses.Remove(DismissibleClass);

        var repeated = callerClasses.Where(IsComponentClass).Distinct(StringComparer.Ordinal).ToList();
        if (repeated.Count > 0)
        {
            throw Failure(
                $"The notice box carries {Quoted(repeated)} more than once or out of place: the "
                + "call site passed a class the component owns. Pass only marker and spacing "
                + "classes.",
                box);
        }

        return (KindsByClass[kindClasses[0]], callerClasses);
    }

    // Dismissible is three things the box must agree with itself about: the
    // class, the close button as the box's direct child, and the click handler
    // on the box. All three, or none. Neither the content nor the close button
    // ever carries a click handler: on a notice that is not dismissible
    // nothing reacts to a click, and on one that is, the box's handler is the
    // only one — the button's click bubbles to it, so both gestures take one
    // path and neither can fire twice.
    private static IElement? ReadDismissibility(IElement box, IElement content)
    {
        if (content.QuerySelectorAll("button").Any(IsCloseButton))
        {
            throw Failure(
                "The close button is inside the notice's content, and so inside what is "
                + "announced.",
                box);
        }

        if (content.HasAttribute(ClickMarker))
        {
            throw Failure("The notice's content carries a click handler of the component's.", box);
        }

        var closeButtons = box.Children.Where(IsCloseButton).ToList();
        var hasClass = ClassesOf(box).Contains(DismissibleClass, StringComparer.Ordinal);
        var hasHandler = box.HasAttribute(ClickMarker);

        if (closeButtons.Count > 1)
        {
            throw Failure("The notice box has more than one close button.", box);
        }

        if (closeButtons.Any(button => button.HasAttribute(ClickMarker)))
        {
            throw Failure(
                "The close button carries a click handler of its own: its click already reaches "
                + "the box's, and a second handler dismisses twice.",
                box);
        }

        var hasButton = closeButtons.Count == 1;
        if (hasButton != hasClass || hasButton != hasHandler)
        {
            throw Failure(
                "The notice box is neither dismissible nor not: "
                + $"close button {Presence(hasButton)}, dismissible class {Presence(hasClass)}, "
                + $"click handler on the box {Presence(hasHandler)}.",
                box);
        }

        return hasButton ? closeButtons[0] : null;
    }

    // The announcement lives on the content and nowhere else: the box is never
    // the live region (the close button would be part of what it says), no
    // element states a live-region attribute, and nothing inside the content
    // is a live region of its own. No role at all means the enclosing region
    // announces.
    private static NoticeAnnouncement ReadAnnouncement(IElement box, IElement content)
    {
        if (box.HasAttribute(RoleAttribute))
        {
            throw Failure(
                $"The notice box itself carries a role ('{box.GetAttribute(RoleAttribute)}'); "
                + "the announcement belongs on its content.",
                box);
        }

        foreach (var element in box.QuerySelectorAll("*").Prepend(box))
        {
            var stated = LiveRegionAttributes.Where(element.HasAttribute).ToList();
            if (stated.Count > 0)
            {
                throw Failure(
                    $"A <{element.LocalName}> in the notice states {Quoted(stated)}; a notice "
                    + "carries no live-region attribute anywhere.",
                    box);
            }

            if (element != content && RolesOf(element).Intersect(LiveRegionRoles).Any())
            {
                throw Failure(
                    $"A <{element.LocalName}> inside the notice is a live region of its own "
                    + $"(role '{element.GetAttribute(RoleAttribute)}').",
                    box);
            }
        }

        return content.GetAttribute(RoleAttribute) switch
        {
            null => NoticeAnnouncement.ByEnclosingRegion,
            "status" => NoticeAnnouncement.Polite,
            "alert" => NoticeAnnouncement.Assertive,
            var role => throw Failure(
                $"The notice's content carries the role '{role}', which is no announcement the "
                + "component renders.",
                box),
        };
    }

    private static List<string> ReadCallerAttributes(IElement box, IReadOnlyList<string> callerClasses)
    {
        var names = new List<string>();

        foreach (var name in box.Attributes.Select(a => a.Name))
        {
            if (name == ClassAttribute)
            {
                if (callerClasses.Count > 0)
                {
                    names.Add(name);
                }
            }
            else if (name == ClickMarker || name.StartsWith(ScopeAttributePrefix, StringComparison.Ordinal))
            {
                // The component's dismiss handler; the stylesheet's scope.
            }
            else if (name.StartsWith(EventMarkerPrefix, StringComparison.Ordinal))
            {
                names.Add(name[EventMarkerPrefix.Length..]);
            }
            else
            {
                names.Add(name);
            }
        }

        return names;
    }

    // ---- Markup ----------------------------------------------------------

    private static IEnumerable<IElement> ElementsOf(IRenderedComponent<IComponent> rendered) =>
        rendered.Nodes.OfType<IElement>().SelectMany(root => root.QuerySelectorAll("*").Prepend(root));

    private static List<IElement> ContentWrappersOf(IElement box) =>
        [.. box.Children.Where(child =>
            child.LocalName == "div"
            && ClassesOf(child).Contains(ContentClass, StringComparer.Ordinal))];

    private static bool IsCloseButton(IElement element) =>
        element.LocalName == "button"
        && ClassesOf(element).Contains(CloseButtonClass, StringComparer.Ordinal);

    private static bool IsComponentClass(string name) =>
        name is BoxClass or DismissibleClass or ContentClass or CloseButtonClass
        || KindsByClass.ContainsKey(name);

    // The attribute's tokens as written, repeats included — a token list that
    // dropped a repeat would hide a class emitted twice.
    private static string[] ClassesOf(IElement element) => TokensOf(element, ClassAttribute);

    private static string[] RolesOf(IElement element) =>
        [.. TokensOf(element, RoleAttribute).Select(role => role.ToLowerInvariant())];

    private static string[] TokensOf(IElement element, string attribute) =>
        element.GetAttribute(attribute)?.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries) ?? [];

    // ---- Failure ---------------------------------------------------------

    private static IElement TheOnly(
        List<IElement> found, string description, IRenderedComponent<IComponent> rendered) =>
        found.Count == 1
            ? found[0]
            : throw new NoticeAssertionException(
                $"Expected one element {description}, found {found.Count}."
                + Environment.NewLine + "Rendered: " + rendered.Markup);

    private static NoticeAssertionException Failure(string message, IElement element) =>
        new(message + Environment.NewLine + "Rendered: " + element.OuterHtml);

    private static string Presence(bool present) => present ? "present" : "absent";

    private static string Quoted(IEnumerable<string> names) =>
        string.Join(", ", names.Select(name => $"'{name}'"));
}
