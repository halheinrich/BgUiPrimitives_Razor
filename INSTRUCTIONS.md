# BgUiPrimitives_Razor

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)

## Stack

C# / .NET 10 / Razor class library (`Microsoft.NET.Sdk.Razor`) / xUnit + bUnit.
Visual Studio 2026 on Windows.

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\BgUiPrimitives_Razor\BgUiPrimitives_Razor.slnx`

## Repo

https://github.com/halheinrich/BgUiPrimitives_Razor — branch `main`.

## Depends on

Standalone, and it stays that way: this library sits below every Razor member
and both hosts, so that no member types the same presentation rule twice.
Nothing in it may know about filters, quizzes, diagrams or XG — a primitive
that needs a domain type is not a primitive and belongs in the member that
owns the type.

It does place obligations on any **host** that renders its components
(see Public API, Host obligations): Bootstrap 5's stylesheet, and a link to
the host's own scoped-CSS bundle.

## Layout

The library and its tests, under `BgUiPrimitives_Razor.slnx`, governed by the repo-root
`Directory.Build.props` (TFM, `TreatWarningsAsErrors`, XML doc generation)
and `Directory.Packages.props` (Central Package Management).

**`BgUiPrimitives_Razor/`** — the Razor class library. Everything public is in
the one namespace `BgUiPrimitives_Razor`, so a consumer's `_Imports.razor`
carries one `@using` however many primitives the library grows. Its occupant:

- **The notice** — `Notice`, the component (markup in `Notice.razor`,
  parameters and the dismissal model in its code-behind, the little CSS
  Bootstrap lacks in `Notice.razor.css`); `NoticeKind` and
  `NoticeAnnouncement`, the user-level selectors its callers name.

**`BgUiPrimitives_Razor.Tests/`** — xUnit + bUnit. Renders the real component
and drives it through real DOM events; the caller-held dismissal path is
exercised through a real parent component, not by re-setting parameters from
the test. Also holds the trim-posture pin.

## Architecture

### What the notice implements

The umbrella's `../SPEC-notices.md` is the ratified model; this library is its
section 3 (how a notice presents) and the component half of section 2 (what a
dismissal is). Read the model for the *why*; this section records how the
component realises it.

The governing idea: **the caller says what the box is, the component owns
what that means.** The decisions are independent, each a user-level selector,
none of them a CSS or ARIA string:

| The caller names | The component owns |
| --- | --- |
| `Kind` — what the notice tells the user | the Bootstrap alert class, and so the colour |
| `Announcement` — politely, assertively, or by an enclosing region | the ARIA role (`status` / `alert` / none), and which element carries it |
| `Dismissible` — whether the user may close it | the close button, the click target, the `alert-dismissible` class |

Colour decides nothing but colour. A yellow box may be polite or assertive,
dismissible or not; what the box *is* decides those (model, section 1).

### Who announces, and what the live region holds

**One decision, not two.** A notice either announces itself — politely or
assertively — or sits inside a live region the caller keeps mounted, which
announces it. In the second case the notice has no urgency of its own to
state: the enclosing region decides that. So "how insistently" and "who
announces" are one selector, `NoticeAnnouncement`, and a contradictory pair
(enclosed *and* assertive) cannot be written, rather than being written and
thrown. `ByEnclosingRegion` renders no live-region role or attribute on any
element: one live region nested in another is announced twice by some screen
readers, and is not what ARIA intends.

**The region is the content wrapper, never the box.** `role="status"` and
`role="alert"` each carry an implicit `aria-atomic="true"` (WAI-ARIA 1.2
§5.4, the two role definitions), and an atomic region is presented as "the
entire contents of the element" (§6.7, `aria-atomic`). With the role on the
box, the close button would be part of what every dismissible notice says.
The button is the box's control, not part of the message, so the role sits
on `div.bg-notice-content` and the button is that region's sibling.

**The live-region attributes belong to the component.** A caller's `role`,
`aria-live`, `aria-atomic` or `aria-relevant` would land on the box and make
it a second live region — around the notice's own, or inside the enclosing
one — so each is refused, under every `Announcement`. That includes
`aria-live="off"`: the way to say "something else announces this" is
`ByEnclosingRegion`.

### One holder per dismissal

A dismissal belongs to one occurrence of one notice, lives exactly as long as
that occurrence, and is never stored. The owner of the occurrence holds it.
The component supports both possible owners and guarantees there is never a
second copy of the bit that could disagree with the first:

- **Component-held** (`Dismissed` unbound). The occurrence dies with the
  page; the caller writes nothing. The component holds the bit and forgets it
  when `OccurrenceKey` changes.
- **Caller-held** (`@bind-Dismissed`). The occurrence outlives the component
  — an app-scoped holder that navigation must not reset. The component keeps
  **no** state: it renders from the bound value and reports the gesture
  through `DismissedChanged`. The owner's value always wins, including an
  owner that declines the report.

Which mode applies is a property of the call site — *which parameters it
supplies*, never their values — so it is read from the `ParameterView` in
`SetParametersAsync`, the only place presence is visible. The combinations
that would let the screen and the owner disagree are refused there with an
`InvalidOperationException`: half a binding, or a binding together with an
`OccurrenceKey`.

A non-dismissible notice **always renders**, whatever dismissed state
arrives: a gate reason or live indicator must never be hidden by a dismissal.

### The close button and the whole box, one handler

A dismissible notice offers the visible close button (keyboard and
screen-reader semantics) and the whole box (the large, low-vision-friendly
target). There is **one** click handler, on the box. The close button
deliberately carries none: its click bubbles to the box, so both gestures
take the same path and neither can fire twice. A non-dismissible box carries
no click handler at all — its callback has no delegate, and the renderer
elides an event attribute whose callback has none.

### Inner interactive content never dismisses

A link or button inside a dismissible notice does its own job. The caller
adds nothing, and there is no authored script. The markup gives the
guarantee and the scoped CSS restores the large target:

- **Markup (the guarantee).** The content is rendered inside a wrapper
  (`div.bg-notice-content`) that stops click propagation while the notice is
  dismissible. A click that *originates* inside the wrapper can never reach
  the box's handler.
- **Scoped CSS (the large target).** Alone, the markup would shrink the target
  to the box's padding. `Notice.razor.css` makes the wrapper and its plain
  content transparent to the pointer (`pointer-events: none`), so a click on
  text lands on the box itself and dismisses; interactive descendants —
  `a`, `button`, `input`, `select`, `textarea`, `label`, `summary`,
  `[tabindex]` — take the pointer back, so their clicks originate inside the
  wrapper and stop there.

The failure mode is the safe one: a host that does not deliver the scoped CSS
gets a notice whose text is not a click target (button and padding still
dismiss), never one whose inner button closes the box.

The wrapper is rendered whether or not the notice is dismissible, so a
caller's selector does not change when `Dismissible` does.

### The caller's attributes

Unmatched attributes are splatted onto the box element itself. The caller's
`class` is merged *after* the component's own classes. `onclick`, `role` and
the live-region attributes (`aria-live`, `aria-atomic`, `aria-relevant`)
belong to the component and are refused with an `InvalidOperationException`.
`onclick` is the Razor silent-splat hazard — the component's would win
without a word. `role` and the live-region attributes are worse than lost:
the component no longer writes a role on the box, so a migrating caller's
`role="alert"` left on the tag would *land*, and wrap the notice's own region
in a second one.

### Trim posture

BgQuiz's WebAssembly client ships this assembly and its publish runs trim
analysis with warnings as errors. The library declares `IsTrimmable` and runs
the trim analyzer in its own build, so a trim-unsafe construct fails *this*
repo's build in the change that introduced it.
`BgUiPrimitivesRazorTrimPostureTests` pins both declarations — the emitted
assembly metadata for `IsTrimmable`, and the project file itself (linked into
the test output) for the analyzer switch, which leaves no trace in the
assembly.

## Public API

Namespace `BgUiPrimitives_Razor`. Consumers add `@using BgUiPrimitives_Razor`
to `_Imports.razor`.

### `NoticeKind`

```csharp
public enum NoticeKind { Information, Warning, Error, Success }
```

Rendered as `alert-info`, `alert-warning`, `alert-danger`, `alert-success`.

### `NoticeAnnouncement`

```csharp
public enum NoticeAnnouncement { Polite, Assertive, ByEnclosingRegion }
```

`Polite` (the default) renders `role="status"`: an outcome the user should
understand. `Assertive` renders `role="alert"`: a gesture that failed, or a
loss about to happen. `ByEnclosingRegion` renders no role and no live-region
attribute: the notice sits inside a live region the caller keeps mounted, and
that region announces it, as insistently as *it* says. The role, when there
is one, is on the content wrapper, not the box.

### `Notice`

```csharp
public sealed partial class Notice : ComponentBase
{
    [Parameter, EditorRequired] public NoticeKind Kind { get; set; }
    [Parameter, EditorRequired] public RenderFragment? ChildContent { get; set; }
    [Parameter] public NoticeAnnouncement Announcement { get; set; }  // default Polite
    [Parameter] public bool Dismissible { get; set; }                 // default false
    [Parameter] public bool Dismissed { get; set; }                   // bind only
    [Parameter] public EventCallback<bool> DismissedChanged { get; set; }
    [Parameter] public object? OccurrenceKey { get; set; }
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    public override Task SetParametersAsync(ParameterView parameters);
}
```

Contracts:

- **`Kind`, `ChildContent`** — required; omitting either is `RZ2012`, an
  error under the house `TreatWarningsAsErrors`.
- **`Dismissible`** — `false` by default. When `false`: no close button, no
  click handler, no `alert-dismissible` class, and any dismissed state is
  ignored — the notice always renders.
- **`Dismissed` / `DismissedChanged`** — supplied together or not at all
  (`@bind-Dismissed`, or `@bind-Dismissed:get` / `@bind-Dismissed:set` over a
  holder's methods). Supplying one without the other throws. When bound, the
  component holds no dismissed state; the gesture raises
  `DismissedChanged(true)` exactly once and the notice is hidden only if the
  bound value comes back `true`. `DismissedChanged` is never raised for a
  component-held dismissal.
- **`OccurrenceKey`** — for a component-held dismissal only. A change
  (compared with `object.Equals`; `null` is a key like any other) forgets the
  dismissal, so the new occurrence shows fresh. Needed only where a new
  occurrence can arrive while the notice stays rendered; a notice that is
  removed from the render tree and rendered again starts fresh regardless.
  Supplying it together with a bound `Dismissed` throws.
- **`Announcement`** — `Polite` by default. Decides the role on the content
  wrapper (`status`, `alert`, or no attribute at all); the box itself never
  carries a role. There is no second parameter it could contradict.
- **`AdditionalAttributes`** — `id`, `style`, `data-*` and the rest land on
  the box element. `class` is merged after the component's classes (blank
  adds nothing). `role`, `onclick`, `aria-live`, `aria-atomic` and
  `aria-relevant` throw, in any casing and under every `Announcement`. Other
  `aria-*` attributes pass through to the box.
- A `Kind` or `Announcement` value outside its enum throws.

Rendered shape (the scope attribute CSS isolation stamps on each element is
omitted):

```html
<!-- dismissible, announces itself -->
<div id="…" class="alert alert-warning alert-dismissible mt-2">
  <div class="bg-notice-content" role="status">…content…</div>
  <button type="button" class="btn-close" aria-label="Dismiss this message"></button>
</div>

<!-- not dismissible, announces itself -->
<div class="alert alert-warning zero-match-notice">
  <div class="bg-notice-content" role="alert">…content…</div>
</div>

<!-- announced by the enclosing region: no role, no aria-live, anywhere -->
<div class="alert alert-success mt-3 mb-0">
  <div class="bg-notice-content">…content…</div>
</div>
```

The box is the element carrying `alert`; the caller's id and marker classes
select it directly. The live region is `div.bg-notice-content`, a direct
child of the box, present in every shape. The close button is
`button.btn-close`, a direct child of the box and the region's sibling. When
dismissed, the component renders nothing.

### Call shapes

A gate reason or live indicator — not dismissible, nothing else to say:

```razor
<Notice Kind="NoticeKind.Warning" Announcement="NoticeAnnouncement.Assertive" class="zero-match-notice">
    No positions matched the filter.
</Notice>
```

A notice inside a live region the caller keeps mounted — the region is
rendered unconditionally and only its content swaps, because a live region
that first appears in the very render that fills it is announced unreliably.
The region announces; the notice adds none of its own:

```razor
<div id="loadedNotice" role="status">
    @if (_loadedName is not null)
    {
        <Notice Kind="NoticeKind.Success" Announcement="NoticeAnnouncement.ByEnclosingRegion"
                class="mt-3 mb-0">
            @_loadedName loaded.
        </Notice>
    }
</div>
```

A notice whose occurrence dies with the page — the component holds the
dismissal and the caller writes no state. `OccurrenceKey` only if a new
occurrence can replace the old one while the notice stays rendered:

```razor
<Notice id="runError" Kind="NoticeKind.Error" Announcement="NoticeAnnouncement.Assertive"
        Dismissible="true" OccurrenceKey="_runNumber">
    @_runError
</Notice>
```

A notice whose occurrence outlives the page — the owner holds the dismissal:

```razor
<Notice id="truncationNotice" class="mt-2" Kind="NoticeKind.Warning" Dismissible="true"
        @bind-Dismissed:get="Notices.IsDismissed(QuizNotice.PickTruncations, Folder.PickOccurrence)"
        @bind-Dismissed:set="_ => Notices.Dismiss(QuizNotice.PickTruncations, Folder.PickOccurrence)">
    …
</Notice>
```

(The holder's member names above are illustrative; the shape is what matters.)

### Host obligations

- **Bootstrap 5.** The component emits `alert`, `alert-info` /
  `alert-warning` / `alert-danger` / `alert-success`, `alert-dismissible`
  and `btn-close`, and owns none of their styling.
- **The host's scoped-CSS bundle.** The library's scoped CSS reaches a host
  as an `@import` inside the host's own `{HostAssembly}.styles.css` — also
  when the host references this library only transitively, through another
  Razor member. The host's root document must link that bundle. A host that
  has never had scoped CSS of its own may not link it yet.

## Pitfalls

- **Migrating a hand-written box.** Delete the old `role="…"`, `aria-live`,
  `@onclick` and `@onclick:stopPropagation` with the markup they sat on; the
  `role`, the live-region attributes and `@onclick` throw if left on the
  `<Notice>` tag, by design. Map the old role to `Announcement` (`status` →
  `Polite`, `alert` → `Assertive`) rather than assuming the default. A box
  that carried **no role** is a question, not a default: if it sits inside a
  live region that stays mounted, it is `ByEnclosingRegion`; if nothing
  announced it, decide whether that was a defect and choose `Polite` or
  `Assertive`. Drop `alert`, `alert-*` and `alert-dismissible` from the
  `class` you pass — they would be emitted twice — and keep only marker and
  spacing classes.
- **The role is not on the box.** A test or selector that reads `role` on the
  element found by the notice's id — `box.GetAttribute("role")`,
  `div.alert-warning[role=alert]` — finds nothing. Read it on the box's
  `div.bg-notice-content` child (`#id > .bg-notice-content`,
  `div.alert-warning > [role=alert]`).
- **`ByEnclosingRegion` is a claim the component cannot check.** It renders
  no role on the caller's word that a mounted live region encloses the
  notice. Used with no such region, the notice is announced by nobody. The
  enclosing region must already be in the DOM before the notice fills it —
  render the region unconditionally and the `<Notice>` conditionally inside.
- **Never nest a self-announcing notice in a live region**, and never reach
  for `aria-live="off"` to quiet one — it throws. Say `ByEnclosingRegion`.
- **Deciding `Dismissible` is a model question, not a colour question.** Use
  the gate-reason test in `../SPEC-notices.md` section 1. The default is
  `false` on purpose.
- **A caller-held dismissal needs both halves.** `Dismissed="@x"` alone and
  `DismissedChanged="@f"` alone both throw. There is deliberately no
  "notify me but hold it yourself" mode: that is two holders.
- **A component-held dismissal lives as long as the component instance.** It
  is lost — correctly — when the notice leaves the render tree. If the
  occurrence must survive navigation, the component is the wrong holder: bind
  `Dismissed` to an app-scoped owner.
- **A replaced occurrence needs `OccurrenceKey`.** If the caller's condition
  stays true while one error replaces another, the notice is the same
  instance and a dismissed one stays dismissed. Name the occurrence.
- **Custom interactive content needs `tabindex`.** An element made clickable
  without being natively interactive (a `span` with `@onclick`) is
  transparent to the pointer inside a dismissible notice unless it carries
  `tabindex` — which it needs anyway to be keyboard-reachable. Prefer a real
  `button`.
- **Clicks inside a dismissible notice's content do not bubble to Blazor
  handlers outside the notice.** The wrapper's stop-propagation is what keeps
  them from dismissing; an ancestor relying on seeing them will not.
- **Text in a dismissible notice cannot be selected with the mouse.** The
  whole box is the dismiss target (ruled), and the pointer transparency that
  makes it so applies to the text. Content the user must be able to copy does
  not belong in a dismissible notice.
- **Do not add a handler to the close button.** Its click already reaches the
  box's handler; a second one reports a caller-held dismissal twice.
- **Editing `Notice.razor.css`.** Its selectors are half of the
  inner-content rule and no bUnit test can execute them. After any change,
  check in a real browser that a click on plain text targets the box and a
  click on an inner link or button targets that element.
- **Adding a `NoticeKind` or `NoticeAnnouncement` value** means adding its arm
  to the component's switch; an enum value without one throws at render.
- **Do not move the role back onto the box, or the close button into the
  wrapper.** Either puts the button's accessible name inside an atomic live
  region, so every dismissible notice would read "Dismiss this message,
  button" after its text.
- **Adding a primitive.** It goes in the root namespace, takes user-level
  selectors, and knows nothing of any domain. If it needs a member's type, it
  belongs in that member.

## Subproject-internal next steps

None open. Adoption of `Notice` by `XgFilter_Razor`, `ExtractFromXgToCsv` and
`BgQuiz_Blazor` is tracked in the umbrella (halheinrich/backgammon#248).
