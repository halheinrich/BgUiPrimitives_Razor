# BgUiPrimitives_Razor

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)
> Open work: the [tracker](https://github.com/halheinrich/backgammon/issues), label `BgUiPrimitives_Razor`, with halheinrich/backgammon#249, halheinrich/backgammon#250 and halheinrich/backgammon#430.

## What it is for

Shared Blazor primitives for the Razor members and hosts, so that no member
writes the same presentation rule, or the same browser-access policy, twice:

- **`Notice`**, the one owner of alert-box markup;
- **`BrowserStorage`** and **`ReportingBrowserStorage`**, the one guarded
  access to `localStorage` and `sessionStorage`, and
  **`BrowserStorageCondition`**, the app's one record that the browser
  refused;
- **`NoticeBox`** and **`BrowserStoragePlan`**, the test side of both, for
  consumers' bUnit tests.

Where to look:

- Writing a `<Notice>` tag or a storage call: Using the library, then Pitfalls.
- A new host: Using the library, Host obligations.
- A consumer's test: Using the library, Test support, and Pitfalls, Consumer
  tests.
- Changing this library: Architecture, for why it has its shape, and Pitfalls,
  Changing this library, for what must change together.

Signatures and per-member contracts are the XML docs'; `Directory.Build.props`
builds them on every build.

## Stack

C# / .NET 10 / Razor class library (`Microsoft.NET.Sdk.Razor`) / a
test-support library for consumers (bUnit + AngleSharp) / xUnit + bUnit.
Visual Studio 2026 on Windows.

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\BgUiPrimitives_Razor\BgUiPrimitives_Razor.slnx`

From this directory:

```bash
dotnet build BgUiPrimitives_Razor.slnx
```

```bash
dotnet test BgUiPrimitives_Razor.slnx
```

The suite is bUnit and reads no `TestData`. The solution also carries
`../BgDataTypes_Lib` and its test support, for the tests alone (see Layout), so
that submodule must be checked out. Nothing here runs in a real browser: there
is no app, so `Notice.razor.css` (see Pitfalls, Changing this library) and the
storage calls' real answers are reached only through a host.

## Repo

https://github.com/halheinrich/BgUiPrimitives_Razor — branch `main`.

## Depends on

Standalone, and it stays that way: this library sits below the Razor members
and hosts that use it. Its primitives are domain-neutral: components, and the
access to the browser that members and hosts share. Nothing in it may know
about filters, quizzes, diagrams or XG — a primitive that needs a domain type
is not a primitive and belongs in the member that owns the type.

It does place obligations on any **host** (see Using the library, Host
obligations): for its components, Bootstrap 5's stylesheet and a link to the
host's own scoped-CSS bundle; for its browser-storage access, one
registration.

## Layout

The library, its test support and its tests, under `BgUiPrimitives_Razor.slnx`, governed by the repo-root
`Directory.Build.props` (TFM, `TreatWarningsAsErrors`, XML doc generation)
and `Directory.Packages.props` (Central Package Management).

**`BgUiPrimitives_Razor/`** — the Razor class library. Everything public is in
the one namespace `BgUiPrimitives_Razor`, so a consumer's `_Imports.razor`
carries one `@using` however many primitives the library grows. Its occupants:

- **The notice** — `Notice`, the component (markup in `Notice.razor`,
  parameters and the dismissal model in its code-behind, the little CSS
  Bootstrap lacks in `Notice.razor.css`); `NoticeKind` and
  `NoticeAnnouncement`, the user-level selectors its callers name.
- **Browser storage** — `BrowserStorage`, the one guarded access to
  `localStorage` and `sessionStorage`; `BrowserStorageArea`, the selector its
  callers name; `BrowserStorageReadResult` and `BrowserStorageWriteResult`,
  how the browser answered; `BrowserStorageCondition`, the app's one record
  that the browser refused a call this visit, and whether its notice was
  dismissed; `ReportingBrowserStorage`, the accessor with every refusal
  reported to that condition and logged; `AddBrowserStorage` and
  `AddReportingBrowserStorage` (`BrowserStorageServiceCollectionExtensions`),
  the host's registrations.

**`BgUiPrimitives_Razor.TestSupport/`** — shipped for **consumers' test
projects** to reference, and for nothing else. It translates between the
library's vocabulary and what the library does in the test renderer, in both
directions, so that a consumer's test never names the library's internals.
Namespace `BgUiPrimitives_Razor.TestSupport`. Its occupants:

- **The notice reader** — `NoticeBox` reads a rendered notice back into the
  component's vocabulary, so that a test says what a notice *is* —
  `NoticeKind`, `NoticeAnnouncement`, dismissible or not, and the attributes
  its call site passed — and never names a class, a role or a selector of the
  component's. `NoticeDismissGesture`, the two gestures as a selector;
  `NoticeAssertionException`, what a failure throws.
- **The storage planner** — `BrowserStoragePlan` plans the browser's answers
  to the real `BrowserStorage` in the accessor's vocabulary — area, key,
  stored, absent, refused, succeeded — and verifies the calls afterwards, so
  that a test never names an interop identifier.
  `BrowserStorageReadAnswer` and `BrowserStorageWriteAnswer`, the answers;
  `BrowserStorageExpectation`, a planned call; `BrowserStorageHeldRead` and
  `BrowserStorageHeldWrite`, a planned call the test holds pending and
  releases; `BrowserStorageAssertionException`, what a failure throws.

References bunit and AngleSharp and no test framework. Not a product
assembly, not trimmable, and declared unsupported in the browser (see
Architecture, The test-support project).

**`BgUiPrimitives_Razor.Tests/`** — xUnit + bUnit. Renders the real component
and drives it through real DOM events; the caller-held dismissal path is
exercised through a real parent component, not by re-setting parameters from
the test. `NoticeTests` asserts through `NoticeBox` wherever the reader
covers what is asserted, so the reader is exercised against the real
component; `NoticeBoxTests` pins the reader against literal markup — the one
test file that spells a notice out — and shows every rule it enforces
failing. `BrowserStorageTests` drives the storage access through bUnit's JS
interop in strict mode: every browser answer is planned for the exact
identifier and arguments an operation must send, so a call nobody planned
fails, and the wire is restated there rather than borrowed from the
implementation or from the planner. `BrowserStoragePlanTests` runs the real
accessor through `BrowserStoragePlan` and shows every rule the planner
enforces failing. `BrowserStorageConditionTests` pins the condition's
occurrence, dismissal and observers. `ReportingBrowserStorageTests` runs the
real accessor through `BrowserStoragePlan` under the reporting access, with
the umbrella's shared test loggers (`RecordingLogger`, `ThrowingLogger`, from
`BgDataTypes_Lib.TestSupport`, which the solution carries for that reason).
`BrowserStorageRegistrationTests` reads the registrations back from the
service collection. Also holds the trim-posture pin
(`BgUiPrimitivesRazorTrimPostureTests`) and `TestSupportPostureTests`.

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
element, so that no live region is nested in another. That nesting risks a
double announcement is an assumption: it has not been tested with a screen
reader, here or on the tracker.

**The region is the content wrapper, never the box.** `role="status"` and
`role="alert"` each carry an implicit `aria-atomic="true"` (WAI-ARIA 1.2
§5.4, the two role definitions), and an atomic region is presented as "the
entire contents of the element" (§6.7, `aria-atomic`). With the role on the
box, the close button would be part of what every dismissible notice says.
The button is the box's control, not part of the message, so the role sits
on `div.bg-notice-content` and the button is that region's sibling.

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

Unmatched attributes are splatted onto the box element itself: `id`, `style`,
`data-*`, and any `aria-*` but the three below. The caller's `class` is merged
*after* the component's own classes, and a blank one adds nothing.

`onclick`, `role` and the live-region attributes (`aria-live`, `aria-atomic`,
`aria-relevant`) belong to the component and are refused with an
`InvalidOperationException`, in any casing and under every `Announcement`.
`onclick` is the Razor silent-splat hazard — the component's would win
without a word. `role` and the live-region attributes are worse than lost:
the component writes no role on the box, so a caller's `role="alert"` would
*land*, and make the box a second live region, around the notice's own or
inside the enclosing one. That includes `aria-live="off"`: the way to say
"something else announces this" is `ByEnclosingRegion`.

### Browser storage access

Hal ruled one guarded access under one policy on halheinrich/backgammon#374
(2026-10-08), because callers writing their own had come to disagree on what a
refusal means; the account is on that thread. `BrowserStorage` is the policy's
one owner. This section records how the type realises the ruling and why it
has its shape.

**It owns the access and the refusal boundary, nothing else.** That is one
interop call per operation, on `localStorage` for `Local` and `sessionStorage`
for `Session` — `ReadAsync` calls `getItem(key)`, `WriteAsync`
`setItem(key, value)`, `RemoveAsync` `removeItem(key)`, and nothing else is
called before or after — and the line between the browser's refusal and every
other failure. Each caller's store keeps parsing, serializing, defaults and
judging whether a stored value is valid. The type holds no state at all, so it
cannot hold an app-wide condition: that is `BrowserStorageCondition`'s, fed by
`ReportingBrowserStorage` (below). Each host keeps what the user is told and
where.

**Every requested operation is attempted.** There is no latch. A refusal
disables nothing: not later reads, writes or removals, not the other area,
not a caller's in-memory value. Nothing is retried either; the next call a
caller makes is simply made. One call's refusal is never an answer for
another call:
- a full quota refuses writes while reads are still served (`setItem` throws
  `QuotaExceededError`: HTML Living Standard,
  https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-setitem);
- each area has its own storage bottle and quota (Storage Standard §4.1,
  https://storage.spec.whatwg.org/), so one area can be full while the other
  is not.

**Outcomes are results.** A read answers `BrowserStorageReadResult`:
- *stored*: `Value`, possibly the empty string;
- *absent*: `getItem` answered null, which it does only for a key with no item;
- *refused*: `Refusal`, the `JSException` exactly as raised.

A write or a removal answers `BrowserStorageWriteResult`, *succeeded* or
*refused*. A removal is a write. Removing a key with no item succeeds, since
`removeItem` then returns without error (same standard,
`#dom-storage-removeitem`). No write reads first, and none has an "absent"
outcome.

**Only the browser's refusal is caught.** A browser that blocks storage
raises its error inside the interop call, and the error reaches .NET as a
`JSException`. That covers a throwing getter too:
- the standard has the `localStorage` and `sessionStorage` getters throw
  `SecurityError` when policy forbids persisting data;
- interop resolves `localStorage.getItem` by property access inside the same
  `try` that reports the call's failure (`beginInvokeJSFromDotNet` calls
  `processJSCall`; read in `blazor.web.js` and `blazor.webassembly.js`
  10.0.10).

Everything else propagates. Three observations decide this boundary, from
probes against the .NET 10 runtime when this type was introduced (recorded in
commit `6efaa9c`'s message; the probes themselves were not kept):
- static rendering's JS runtime throws `InvalidOperationException` for any
  interop call;
- `JSDisconnectedException` is not a `JSException`;
- a call that outlives the runtime's default interop timeout fails with
  `TaskCanceledException`, an `OperationCanceledException` (the control, with
  no timeout, stayed pending).

None of these is an answer from the browser. `BrowserStorageTests` pins the
library's half, that each propagates for every operation and area; it injects
the exception, and establishes nothing about the runtime.

**Usage errors throw at the call.** An undeclared `BrowserStorageArea`, a null
key or a null value throws synchronously, before a task exists and before
any interop. That is the task-based asynchronous pattern's rule for usage
errors, and they never become results. A null value is refused rather than
stored as the string `"null"`; storing nothing is a removal. Any other string
is a key or a value, the empty string included.

**The shape, and what was rejected.**

- *One type, with the area named per call.* `BrowserStorageArea` is the
  user-level selector, and the type maps it to the browser object in one
  place. An instance per area would need two registrations told apart by
  keyed services, and would buy a store nothing: a store uses one area and
  keeps it beside its key.
- *Sealed, immutable result classes, produced only here.*
  - Not structs, whose `default` would be a fabricated outcome.
  - The flags carry `[MemberNotNullWhen]`, so `Value` after `IsStored` and
    `Refusal` after `IsRefused` are non-null to the compiler. This is the
    shape of ASP.NET Core's `AuthenticateResult`.
  - No outcome enum beside the flags. It would be a second way to ask one
    question, and a switch over it would still need a discard arm.
  - No closed class hierarchy for pattern matching. C# cannot check that
    switch for exhaustiveness either, and the flags read as plainly.
  - Equality is reference equality. An outcome is read through its flags,
    and a refusal carries an exception, which has no value equality.
- *`Task<T>`, not `ValueTask<T>`.* `Task` is the pattern's default. A caller
  may hold one or await it twice, and storage is not a hot path.
- *No `CancellationToken`.* A Web Storage call is synchronous in the browser,
  so there is nothing long-running to cancel. The runtime's default interop
  timeout still applies, and it propagates as a cancellation.
- *The browser's own functions, not a JavaScript module.* There is no asset
  for a host to serve, and no module import that could fail and be mistaken
  for a refusal.
- *Registered by `AddBrowserStorage`, not by each host's
  `AddScoped<BrowserStorage>()`* (BgFolderAccess_Razor's form). The lifetime
  is the library's decision: an access must not outlive the `IJSRuntime` of
  its own scope, which a server scopes per circuit. So the library decides it
  once, with `TryAdd`, and a second call is harmless.
  - The constructor stays public: DI activation needs it, and a test may
    supply its own runtime.
  - The extension lives in this library's one namespace, not in
    `Microsoft.Extensions.DependencyInjection`, where Microsoft recommends
    extension methods go. Everything public here is in one namespace, and
    this library declares nothing in a namespace it does not own.

**The condition and the reporting access.** The host-side half of this
policy — whether storage is unavailable, how a refusal is reported, the
notice's dismissal (`SPEC-notices.md` §2) — is common to the hosts, and Hal
ruled that common code moves to one shared owner
(halheinrich/backgammon#424, 2026-10-19, comment 6262547637). This library
owns the accessor and its results, so it owns the condition and the access
that reports to it.

- *`BrowserStorageCondition` is the one fact.* One per loaded app. The first
  refusal reported begins the occurrence; every later one is the same
  occurrence, so a dismissal holds across navigation and further refusals. A
  dismissal before any occurrence throws, since it would hide the occurrence
  before it began. Nothing consults it before a call: it records, and gates
  nothing. A page attaches an observer, is told the current state at once and
  each change after, and re-renders. Not thread-safe: every member runs on
  the renderer's synchronization context.
- *The condition holds no logger.* It is one fact however it arose. What a
  refusal costs is the caller's to say, and only the caller knows it.
- *`ReportingBrowserStorage` says it, once per refusal.* Each operation takes
  the area, the key and the caller's **consequence phrase**, required and
  non-blank — what the refusal means for that caller, such as "the mix panel
  starts blank for this visit". It makes the accessor's one call. On a
  refusal it reports to the condition, then logs one warning carrying the
  browser's exception, under one message template, which the phrase finishes:
  `The browser refused a storage {Operation} of the key {Key}; {Consequence}.`
  (`Operation` is `read`, `write` or `removal`). It returns the accessor's own
  result unchanged. A success reports and logs nothing.
- *Containment.* The logging call alone is wrapped. A logger that throws is
  contained there, after the occurrence is recorded, so a failing logging
  provider costs neither the occurrence nor the result.
- *The accessor's boundary is unchanged.* Anything `BrowserStorage` throws or
  propagates — an argument error, the platform refusing interop, a timeout —
  propagates from here unchanged, reported to nothing.
- *Results, not values.* A caller that must tell a refusal from nothing
  stored reads the result; the common caller reads
  `IsStored ? Value : null`. No latch, retry or default is added.
- *Registered by `AddReportingBrowserStorage`.* It registers the access and
  the condition scoped with `TryAdd`, and calls `AddBrowserStorage` itself,
  so a host cannot register the condition without the accessor beneath it.
  Scoped means one condition per loaded app, per circuit on a server.

### Trim posture

BgQuiz's WebAssembly client ships this assembly and its publish runs trim
analysis with warnings as errors. The library declares `IsTrimmable` and runs
the trim analyzer in its own build, so a trim-unsafe construct fails *this*
repo's build in the change that introduced it. `BrowserStorage` sends and
receives only `string` through interop (keys, values and `getItem`'s answer),
so no library type is ever an interop type argument.
`BgUiPrimitivesRazorTrimPostureTests` pins the declarations — the emitted
assembly metadata for `IsTrimmable`, and the project file itself (linked into
the test output) for both switches, the analyzer's leaving no trace in the
assembly.

### The test-support project

**Why it exists.** A green build proves nothing about a `<Notice>` tag. A
parameter name the component does not have — misspelt, or renamed under the
caller — compiles clean, is splatted onto the box as a plain attribute, and
the real parameter silently takes its default: polite, not dismissible.
XgFilter_Razor `2f46cca` (2026-09-20) records the case: a renamed parameter
left both its assertive boxes silently polite under a green build.
`NoticeBoxTests.ShouldBe_Fails_OnAMisspeltParameter_SplattedOntoTheBox`
reproduces the trap through the real component. Adopters answered with test
helpers of their own, and each helper knew this component's internals. The
producer owns that knowledge, so the producer ships the reader.

**Parse, then compare.** A `NoticeBox` cannot be obtained for anything but a
well-formed notice: every way in reads the whole box and throws unless it is
what the component renders. The structural rules — each component class
exactly once; the announcement on the content and nowhere else, no live-region
attribute anywhere in the box, and no live region nested in the content; the
close button outside the content; class, close button and box click handler
all present or all absent; never a click handler on the content or on the
close button — are therefore not something a consumer can forget to assert.
`ShouldBe` then compares what was read with what the call site asked for, and
states everything at once with nothing optional, because the failure it exists
for changes a value the test did not think to check.

**"Reacts to no click" is read, not clicked.** The reader looks for the test
renderer's click-handler marker on the box and the content rather than
clicking and expecting `MissingEventHandlerException`. A click bubbles: inside
a consumer's page it would reach — and run — a handler on any ancestor, so
the clicking form both fails falsely there and has side effects in an
assertion. The marker is what bUnit dispatches on, so its absence is the same
fact; `NoticeTests.NonDismissible_ReactsToNoClick` pins the behaviour itself
against the real component.

**Finding takes the caller's identifiers, never the component's.** `ById` and
`ByMarkerClass` take what the call site wrote on the tag, and `ByMarkerClass`
refuses a class the component owns. `From` admits an element the test found
some other way of its own. `AllIn` is how a test says "no notice is shown"
without knowing what one looks like. Whether an *element with the caller's
id* exists is the caller's own question and needs no helper
(`cut.FindAll("#runError")`).

**An independent reader.** The classes, roles and markers are restated in
`NoticeBox` rather than shared with `Notice` through `InternalsVisibleTo`. A
reader that took its expectations from the implementation would agree with
whatever the implementation did, and the product assembly would name a test
assembly as a friend. The restatement is held honest from both sides:
`NoticeTests` renders the real component through the reader for every
combination, and `NoticeBoxTests` pins the reader against literal markup.

**Dependencies, decided.** bunit, because a gesture is an action and only the
test renderer can dispatch a click, because the finders take what it
renders, and because the storage planner answers calls made through its JS
interop; AngleSharp directly, because the public surface is written in its
types. **No test framework:** a failure is `NoticeAssertionException` or
`BrowserStorageAssertionException`, this assembly's own, which every
framework reports as a failed test with its message. An xunit reference
here would ride into every consumer's test project, and xunit's next major
is a different package identity declaring the same `Xunit.Assert` — a
consumer moving to it would collide with the one pinned here. Consumers reach
this project by `ProjectReference`, so the bunit and AngleSharp versions in
this repository's `Directory.Packages.props` are the floor for theirs: a bump
here is coordinated through the umbrella.

**Never in a product.** The assembly is declared
`[UnsupportedOSPlatform("browser")]` (in its project file). Every project
that can reach a browser publish declares the browser platform — each Razor
member in its project file, each WebAssembly client through its SDK — and in
such a project the platform-compatibility analyzer reports any use of this
assembly as `CA1416`, an error under the house `TreatWarningsAsErrors`
(measured in a scratch project when introduced: error with the browser
platform declared, clean without; no record of the run was kept). Test
projects declare no browser platform and are unaffected.
`TestSupportPostureTests` pins the declaration, not the analyzer's verdict; it
also pins that the assembly claims no `IsTrimmable`, that it references no
test framework, and that the library references no project. **Stated limit:**
a server host declares no browser platform and is not stopped by the analyzer.
That a member's non-test project never references this one is a claim about
other repositories, and the umbrella gates it (`../tools/check-members.sh`,
rule 8).

### The storage planner

**Why it exists.** `BrowserStorage` reaches the browser through interop
calls, and a bUnit test of code that stores anything must answer them.
Spelled in each consumer's tests, those calls would be this library's
interop representation restated in three repositories, where a change here
cannot reach it (halheinrich/backgammon#377). The library owns the
representation, so it owns the test-side translation too. A consumer's test
states areas, keys and answers in the accessor's vocabulary, and
`BrowserStoragePlan` turns them into answers to the real calls.

**The real accessor runs.** The plan is a pair of bUnit invocation handlers,
registered with `BunitJSInterop.AddInvocationHandler`. They answer the calls
the real `BrowserStorage` makes. Nothing stands in for the accessor — no fake
`IJSRuntime`, no interface over it — so a consumer's test sees the accessor's
own results. A refusal reaches the accessor as a `JSException`, as a
browser's does, so the accessor's catch boundary runs in every consumer test
that plans one.

**Every storage call is the plan's, and nothing else is.**
- The handlers match the six storage identifiers and no others. Every other
  interop plan, a test's catch-all included, and bUnit's invocation record are
  left as they were.
- They are not catch-all handlers. bUnit tries every non-catch-all handler
  before any catch-all (`BunitJSInterop.TryGetHandlerFor`,
  `src/bunit/JSInterop/BunitJSInterop.cs` at bUnit's tag `v2.8.6`), so a
  catch-all the test registers for its own calls never takes a storage call
  from the plan.
- The plan answers storage calls in either `JSRuntimeMode`. An unexpected
  call — nothing declared for it, one more than declared, or a different
  operation, area, key or written value — fails at the call with
  `BrowserStorageAssertionException`. That is not a `JSException`, so the
  accessor propagates it rather than reading it as a refusal. The call is also
  recorded, so `Verify` reports it even when the code under test caught the
  exception.
- `Verify` also reads bUnit's record of every invocation, and reports any
  storage call the plan did not answer. One comes from a handler the test
  registered itself, which spells an identifier; another is a call with a
  result type no operation has.
- One plan per `BunitJSInterop`. A second would take every call from the
  first, so `On` refuses it.

**Registration: `BrowserStoragePlan.On(JSInterop)`.** A static factory, not a
constructor, because creating a plan registers handlers on the test's
runtime, a side effect a constructor would hide. It takes the
`BunitJSInterop`, the narrowest thing it uses, rather than the whole
`BunitContext`. The test registers `BrowserStorage` itself, as a host does
(`Services.AddBrowserStorage()`): the plan answers the runtime and does not
reach into the service collection.

**Matched, not sequenced.** A call is answered by the earliest-declared
expectation, not yet used up, with the same operation, area, key and — for a
write — value. So successive answers for one call are declared in the order
they are given: a refusal and then a success, or two overlapping reads of
one key. Calls to different keys, areas or values may arrive in any order.
Writes match by value, so two saves of different values are told apart
however they interleave. Order is checked only where a test names a
sequence with `RequireOrder`.

**Each answer is declared with its call.** Each `Expect…` method takes its
answer as an argument. A fluent builder was rejected because it allows a
half-declared expectation, `ExpectRead(…)` with its answer forgotten. The
answer types are classes with fixed instances, not enums, so no answer
outside the browser's own can be written. `Stored(null)` throws: a key with
no item is `Absent`.

**Held calls are reached, pending, then released.** `ExpectHeld…` returns a
handle. Once the code under test makes the call, it stays pending until the
test calls `Release` with an answer. Each held expectation is its own call,
so overlapping calls are told apart and may complete in any order, the older
after the newer. bUnit's own handler can do none of this
(`src/bunit/JSInterop/InvocationHandlers/JSRuntimeInvocationHandlerBase{TResult}.cs`
at bUnit's tag `v2.8.6`, the version this repository pins):
- it answers every invocation it matches from one shared pending task;
- it fails that task after `BunitContext.DefaultWaitTimeout`.

A held call has no timeout, deliberately. It is pending for as long as the
test says, and a timer would make the result depend on how fast the test
ran. Its completion runs the awaiting code's continuation asynchronously,
never inside `Release`, which is why a test must wait for that continuation
(Pitfalls, Consumer tests).

**Verified explicitly.** `Verify` is a call the test makes, as `ShouldBe` is.
Verifying at disposal was rejected: it would throw from `Dispose`, and it
would run for a test that had already failed. A held call that was reached
counts as made, released or not. Whether the code under test then completed
is the test's own assertion.

**An independent restatement.** The identifiers are restated in
`BrowserStoragePlan`, not shared with `BrowserStorage` through
`InternalsVisibleTo`, for `NoticeBox`'s reason. Three places spell the wire,
each for its own purpose:
- `BrowserStorage` makes the calls;
- `BrowserStoragePlan` answers them;
- `BrowserStorageTests` pins them with literals of its own, and takes
  nothing from the plan it would otherwise be validating. The plan exposes
  no identifier for it to take.

`BrowserStoragePlanTests` holds the plan to the accessor by running the real
one through it.

**What it is not.** It is no simulated storage: a plan holds no items, and a
write does not make a later read stored. Each answer is the test's
statement of what the browser says. Nor does it need production API of its
own: it reaches the accessor only through the interop calls the accessor
already makes.

## Using the library

Namespace `BgUiPrimitives_Razor`. Consumers add `@using BgUiPrimitives_Razor`
to `_Imports.razor`, and a host's `Program.cs` adds `using BgUiPrimitives_Razor;`
for `AddBrowserStorage` and `AddReportingBrowserStorage`. The test-support
surface, in `BgUiPrimitives_Razor.TestSupport`, is at the end of this section.

### A notice

What a call site decides:

- **`Kind` and `ChildContent`** are required; omitting either is `RZ2012`, an
  error under the house `TreatWarningsAsErrors`. `Kind` is `Information`,
  `Warning`, `Error` or `Success`, and decides nothing but the colour.
- **`Announcement`**, by what the notice reports and where it sits
  (`../SPEC-notices.md` section 3): `Polite`, the default, for an outcome the
  user should understand; `Assertive` for a gesture that failed or a loss
  about to happen; `ByEnclosingRegion` inside a live region the caller keeps
  mounted.
- **`Dismissible`**, `false` by default. When `false`: no close button, no
  click handler, no `alert-dismissible` class, and any dismissed state is
  ignored — the notice always renders.
- **Who holds the dismissal** (Architecture, One holder per dismissal). A
  caller that holds it binds `@bind-Dismissed`, or `@bind-Dismissed:get` and
  `@bind-Dismissed:set` over a holder's methods. The gesture then raises
  `DismissedChanged(true)` exactly once, and the notice is hidden only if the
  bound value comes back `true`.

Everything else on the tag lands on the box (Architecture, The caller's
attributes).

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

This shape is documented for hosts, stylesheets and this repository. **A
consumer's bUnit test does not read it:** it asserts through `NoticeBox`
(below).

Call shapes. A gate reason or live indicator — not dismissible, nothing else
to say:

```razor
<Notice Kind="NoticeKind.Warning" Announcement="NoticeAnnouncement.Assertive" class="zero-match-notice">
    No positions matched the filter.
</Notice>
```

A notice inside a live region the caller keeps mounted — the region is
rendered unconditionally and only its content swaps, because a live region
that first appears in the very render that fills it may not be announced. That
rests on one observation of one box (halheinrich/backgammon#171), not on a
screen-reader test, and a self-announcing notice rendered under a condition is
just such a region (halheinrich/backgammon#249). The region announces; the
notice adds none of its own:

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

### Browser storage

Inject `ReportingBrowserStorage`, and each refusal reaches
`BrowserStorageCondition` and the log before the result returns. `BrowserStorage`
takes the same arguments, less the consequence phrase, answers the same
results and reports nothing: it is for a caller that reports refusals its own
way, as `XgFilter_Razor`'s filter storage does through its host's sink.

Call shapes. `Storage` is the injected `ReportingBrowserStorage`; the store's
member names are illustrative. A store restores a value under its own parser
and default:

```csharp
var read = await Storage.ReadAsync(BrowserStorageArea.Local, StorageKey,
    "the defaults stand for this visit");
if (read.IsStored && TryParse(read.Value, out var restored))
{
    Apply(restored);                       // invalid stored data is this store's to judge
}
```

A store saves a change the screen has already made; a refusal is reported
without undoing the change:

```csharp
_settings = changed;
await Storage.WriteAsync(BrowserStorageArea.Local, StorageKey, ToJson(changed),
    "the settings hold for this visit only");
```

A page draws the app's one notice from the condition. It attaches in a
lifecycle method, because a refusal can arrive while no gesture of the page is
running, and disposes the attachment with the page. The condition holds the
dismissal, so it is bound; the id and the wording are the host's:

```csharp
_storageAttachment = StorageCondition.Attach(() => _ = InvokeAsync(StateHasChanged));
```

```razor
@if (StorageCondition.HasOccurred)
{
    <Notice id="storageUnavailableNotice" Kind="NoticeKind.Warning" Dismissible="true"
            @bind-Dismissed:get="StorageCondition.IsNoticeDismissed"
            @bind-Dismissed:set="_ => StorageCondition.DismissNotice()">
        …
    </Notice>
}
```

### Host obligations

- **Bootstrap 5.** The component emits `alert`, `alert-info` /
  `alert-warning` / `alert-danger` / `alert-success`, `alert-dismissible`
  and `btn-close`, and owns none of their styling.
- **The host's scoped-CSS bundle.** The library's scoped CSS reaches a host
  as an `@import` inside the host's own `{HostAssembly}.styles.css` — also
  when the host references this library only transitively, through another
  Razor member. The host's root document must link that bundle. A host that
  has never had scoped CSS of its own may not link it yet.
- **Registering browser storage**, when anything the host renders or
  constructs injects `BrowserStorage`: `builder.Services.AddBrowserStorage();`;
  when it injects `ReportingBrowserStorage` or `BrowserStorageCondition`:
  `builder.Services.AddReportingBrowserStorage();`, which also registers
  `BrowserStorage`. Register it in every project that creates such a component
  or service. In a Blazor Web App that means the client, and also the server if
  the server prerenders or runs that component. A missing registration fails
  when the component or service is created.

### Test support — `BgUiPrimitives_Razor.TestSupport`

For a consumer's **test** project only, by `ProjectReference` to
`BgUiPrimitives_Razor.TestSupport/BgUiPrimitives_Razor.TestSupport.csproj`.
(XgFilter_Razor's own test support builds on it, under the same rule.) It brings
bunit and AngleSharp at this repository's versions and no test framework. Both
tools work on bUnit's renderer; a Playwright e2e test, in a real browser, has
neither.

#### The notice reader

`NoticeBox` finds a notice (`From`, `ById`, `ByMarkerClass`, `AllIn`), reads it
(`Kind`, `Announcement`, `Dismissible`, `CallerClasses`, `CallerAttributes`,
`Element`, `Content`), asserts it (`ShouldBe`) and acts on it (`Dismiss`). A
failure is `NoticeAssertionException`.

- **Every way in reads the whole box**, and throws on an element that is not a
  notice box (the identifier is on something else, or the box is hand-written)
  and on a box that breaks a structural rule (Architecture, Parse, then
  compare). A call site that still passes `alert` or an `alert-*` class fails
  here.
- **`ById` / `ByMarkerClass`** — exactly one element must carry the
  identifier. Any `IRenderedComponent<T>` converts to the parameter type.
- **`AllIn`** — every notice, in document order; empty when none is shown.
  Hand-written alerts are not notices and are not returned.
- **`ShouldBe`** — all of it, every time: the call site's `Kind`, its
  `Announcement` (`Polite` if it names none), its `Dismissible` (`false` if it
  names none), and the names of the non-parameter attributes on the tag, as
  written — `"id"`, `"class"`, `"style"`, `"data-…"`, `"onmouseover"` for a
  bound `@onmouseover`. The box must carry exactly those: one it carries that
  was not named is the silent-splat trap, and one named that it does not carry
  was lost. Names are matched case-insensitively. The scope attribute, the
  test renderer's event markers and the component's own `class` are the
  reader's to know and are never named. Every difference is stated in one
  message, with the rendered box. Returns the box, for chaining.
- **`Dismiss`** — a real click through the test renderer on the close button
  or the box. Throws `NoticeAssertionException` on a notice that is not
  dismissible. Asserts nothing about what follows — a caller-held dismissal
  may decline. It returns `void`, so a test cannot await the dismissal: while
  earlier work holds the renderer the click is queued, and `Dismiss` returns
  before the notice has gone (halheinrich/backgammon#387).
- **A `NoticeBox` is a reading of one render.** After `Dismiss`, or anything
  else that re-renders, find the notice again.
- **Values are the caller's to read** — `box.Element.GetAttribute("style")`,
  `box.Content.TextContent`, `box.CallerClasses`.

Call shapes, in a consumer's test. A dismissible assertive error, with both
gestures as a theory:

```csharp
public static TheoryData<NoticeDismissGesture> BothGestures =>
    new(Enum.GetValues<NoticeDismissGesture>());

[Theory]
[MemberData(nameof(BothGestures))]
public async Task SaveError_IsADismissibleAssertiveError_AndDismisses(NoticeDismissGesture gesture)
{
    var cut = await RenderWithARefusedSaveAsync();

    // <Notice id="filterSaveError" class="mb-4" style="max-width:800px"
    //         Kind="NoticeKind.Error" Announcement="NoticeAnnouncement.Assertive"
    //         Dismissible="true" …>
    var box = NoticeBox.ById(cut, "filterSaveError").ShouldBe(
        NoticeKind.Error, NoticeAnnouncement.Assertive, dismissible: true,
        "id", "class", "style");
    Assert.Equal(["mb-4"], box.CallerClasses);

    box.Dismiss(gesture);

    Assert.Empty(cut.FindAll("#filterSaveError"));
}
```

A notice announced by its enclosing region — no live-region role or attribute
anywhere in the box is part of what `ByEnclosingRegion` asserts; the region
itself is the caller's element and the caller's to check:

```csharp
// <div id="loadedNotice" role="status">
//     <Notice Kind="NoticeKind.Success" Announcement="NoticeAnnouncement.ByEnclosingRegion"
//             class="mt-3 mb-0">…</Notice>
// </div>
var region = cut.Find("#loadedNotice");
Assert.Equal("status", region.GetAttribute("role"));

var box = Assert.Single(NoticeBox.AllIn(cut)).ShouldBe(
    NoticeKind.Success, NoticeAnnouncement.ByEnclosingRegion, dismissible: false,
    "class");
Assert.True(region.Contains(box.Element));
```

A gate reason that cannot be closed needs nothing more than `dismissible:
false` — that the box and its content react to no click is part of what it
means.

#### The storage planner

`BrowserStoragePlan.On(JSInterop)` puts a plan on the test's runtime, and the
test registers `BrowserStorage` as a host does. `ExpectRead`, `ExpectWrite` and
`ExpectRemove` each declare one call in the accessor's terms with the browser's
answer — `BrowserStorageReadAnswer.Stored(value)`, `.Absent`, `.Refused`;
`BrowserStorageWriteAnswer.Succeeded`, `.Refused` — given at the call, so the
accessor's operation has completed when it returns. `times` (default 1)
declares that many identical calls, each answered alike. `ExpectHeldRead`,
`ExpectHeldWrite` and `ExpectHeldRemove` declare a call the test holds pending
and completes with `Release`; `IsReached` says whether it was made.
`RequireOrder` names a sequence. `Verify` ends the test; it changes nothing and
may be called more than once. A failure is `BrowserStorageAssertionException`,
and each type's `ToString` describes it for the message.

A planned refusal reaches the accessor as a `JSException` raised for that one
call, so the result is refused. The test cannot reach that exception: a store
that logs the refusal with its exception attached cannot be asserted to log the
one raised (halheinrich/backgammon#378).

Call shapes, in a consumer's test. The store's key is the store's own to
name; the interop calls are not:

```csharp
public class FilterStoreTests : BunitContext
{
    private const string StorageKey = "bg.filter";

    private readonly BrowserStoragePlan _storage;

    public FilterStoreTests()
    {
        Services.AddBrowserStorage();
        _storage = BrowserStoragePlan.On(JSInterop);
    }

    [Fact]
    public void ARefusedRestore_KeepsTheDefaults()
    {
        _storage.ExpectRead(BrowserStorageArea.Local, StorageKey, BrowserStorageReadAnswer.Refused);

        var cut = Render<FilterPanel>();

        // … what the store and the host do with a refusal …
        _storage.Verify();
    }
}
```

An older save completing after a newer one. A test of a held call asserts only
after the continuation it is about has finished (Pitfalls, Consumer tests).
Each Apply is a click, and `ClickAsync` returns a task that completes when the
event handler is done (bUnit 2.8.6's documentation of
`EventHandlerDispatchExtensions.ClickAsync`), so the test keeps both tasks and
finishes them one at a time:

```csharp
var older = _storage.ExpectHeldWrite(BrowserStorageArea.Local, StorageKey, jsonA);
var newer = _storage.ExpectHeldWrite(BrowserStorageArea.Local, StorageKey, jsonB);

// … select A …
var applyA = cut.Find("#applyFilter").ClickAsync(new MouseEventArgs());
// … select B …
var applyB = cut.Find("#applyFilter").ClickAsync(new MouseEventArgs());
Assert.True(older.IsReached);
Assert.True(newer.IsReached);

newer.Release(BrowserStorageWriteAnswer.Succeeded);
await applyB.WaitAsync(BunitContext.DefaultWaitTimeout);

older.Release(BrowserStorageWriteAnswer.Refused);
await applyA.WaitAsync(BunitContext.DefaultWaitTimeout);   // A's continuation has run

// … B is still current …
_storage.Verify();
```

A restoration held pending across a change, then released. The read starts
in a lifecycle method, so there is no task of it to keep. The test waits
instead for an observation that only the restoration's continuation
produces, and bounds that wait too:

```csharp
var restore = _storage.ExpectHeldRead(BrowserStorageArea.Local, StorageKey);
var cut = Render<FilterPanel>();
Assert.True(restore.IsReached);

// … change the source while the read is pending …

restore.Release(BrowserStorageReadAnswer.Stored(savedJson));
await Owner.RestorationSettled.WaitAsync(BunitContext.DefaultWaitTimeout);

// … the newer draft stands …
_storage.Verify();
```

(The component, the selector, the owner's `RestorationSettled` signal, the
store's key and the JSON are illustrative. The signal stands for whatever
the code under test exposes that completes when its restoration has
settled, including a restoration that rejected a stale result.)

## Pitfalls

### Writing a notice

- **Deciding `Dismissible` is a model question, not a colour question.** Use
  the gate-reason test in `../SPEC-notices.md` section 1. The default is
  `false` on purpose.
- **`ByEnclosingRegion` is a claim the component cannot check.** It renders
  no role on the caller's word that a mounted live region encloses the
  notice. Used with no such region, the notice is announced by nobody. The
  enclosing region must already be in the DOM before the notice fills it —
  render the region unconditionally and the `<Notice>` conditionally inside. A
  dismissible one puts its close button inside that region, so the button's
  name is likely part of what is announced (unmeasured;
  halheinrich/backgammon#249).
- **Never nest a self-announcing notice in a live region**, and never reach
  for `aria-live="off"` to quiet one — it throws. Say `ByEnclosingRegion`.
- **The role is not on the box.** A test or selector that reads `role` on the
  element found by the notice's id — `box.GetAttribute("role")`,
  `div.alert-warning[role=alert]` — finds nothing. A test does not read the
  role at all: it states the `NoticeAnnouncement` to `NoticeBox.ShouldBe`.
- **A caller-held dismissal needs both halves.** `Dismissed="@x"` alone and
  `DismissedChanged="@f"` alone both throw. There is deliberately no
  "notify me but hold it yourself" mode: that is two holders.
- **A component-held dismissal lives as long as the component instance.** It
  is lost — correctly — when the notice leaves the render tree. If the
  occurrence must survive navigation, the component is the wrong holder: bind
  `Dismissed` to an app-scoped owner.
- **A replaced occurrence needs `OccurrenceKey`.** If the caller's condition
  stays true while one error replaces another, the notice is the same
  instance and a dismissed one stays dismissed. Name the occurrence (compared
  with `object.Equals`; `null` is a key like any other).
- **Custom interactive content needs `tabindex`.** An element made clickable
  without being natively interactive (a `span` with `@onclick`) is
  transparent to the pointer inside a dismissible notice unless it carries
  `tabindex` — which it needs anyway to be keyboard-reachable. Prefer a real
  `button`.
- **A click on interactive content in a dismissible notice does not bubble to
  Blazor handlers outside the notice.** The wrapper's stop-propagation is what
  keeps it from dismissing; an ancestor relying on seeing it will not. A click
  on plain text lands on the box, and does bubble.
- **Text in a dismissible notice cannot be selected with the mouse.** It is
  the ruled price of the whole box being the dismiss target
  (`../SPEC-notices.md` section 6, E). Content the user must be able to copy
  does not belong in a dismissible notice.
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

### Browser storage

- **Do not latch around browser storage.** A caller that skips its next call
  after a refusal re-creates what was ruled out. One example is a "storage
  unavailable" flag consulted before calling. Report each refusal to
  `BrowserStorageCondition` (`ReportingBrowserStorage` does) and keep calling.
  The condition remembers that a refusal *happened*, for the notice, and gates
  nothing; a member that consulted it before a call, or a second holder beside
  it, would be the per-app latch again.
- **`read.Value ?? fallback` loses the refusal.** It yields the right value
  and drops the fact. On the plain `BrowserStorage`, a store whose host reports
  refusals checks `IsRefused` first; `ReportingBrowserStorage` has already
  reported it.
- **Parse outside the storage result.** Read first, then parse `Value` under
  your own parser's catch. A parse failure is invalid stored data, which is
  the store's to handle; it never belongs in the refused branch. A
  `catch (JSException)` left around a storage call is dead code, so remove it
  when migrating.
- **Migrating an unguarded call changes what follows it.** An awaited write
  that used to throw on refusal now returns, and the code after it runs.
  That is the point: a refused write must not stop unrelated work. Decide at
  each call site what the refusal means; usually that is nothing beyond the
  host's report.
- **Storage calls need interop.** Static rendering cannot make interop calls.
  A storage call there throws `InvalidOperationException`, which propagates,
  because it is not a refusal. Call from `OnAfterRenderAsync` or an event
  handler.
- **Register browser storage through `AddBrowserStorage` or
  `AddReportingBrowserStorage`, not by hand.** The lifetime is part of the
  contract. On a server, a singleton would hold the first circuit's runtime.

### Consumer tests

- **A consumer's bUnit test that names `bg-notice-content`, `btn-close`, an
  `alert-*` class or a role is a defect — use `NoticeBox`.** That includes a
  selector (`#id > .bg-notice-content`, `button.btn-close`), a class string
  (`"alert-danger"`), a role string (`"status"`), a click on the close button
  by selector, and a hand-rolled list of the attributes the renderer stamps.
  Each is this component's internals restated where a change here cannot
  reach it. The caller's own `id`, marker classes and attribute values are
  not the component's and are fine. XgFilter_Razor's tests still carry such a
  helper (halheinrich/backgammon#250), and BgQuiz_Blazor's `PageTests` such
  clicks.
- **Assert every notice with `ShouldBe`, including the ones "obviously
  right".** The silent-splat trap changes nothing the compiler or a casual
  test sees. And write `ShouldBe`'s arguments from what the call site is
  *meant* to say, not from what it renders.
- **The test-support project is for test projects.** No product project —
  no Razor member, no host, no WebAssembly client — may reference it: it
  carries bunit. A project that declares the browser platform fails to build
  with `CA1416` if it uses it, and the umbrella's `check-members.sh` gates the
  rest (Architecture, The test-support project).
- **A consumer's bUnit test that names `localStorage`, `sessionStorage`,
  `getItem`, `setItem` or `removeItem` is a defect — use
  `BrowserStoragePlan`.** That includes a `JSInterop.Setup` or `SetupVoid` for
  one of them, and a read of `JSInterop.Invocations` by one of them. Each
  restates this library's interop where a change here cannot reach it. A
  handler that answers a storage call in the plan's place also fails `Verify`.
  The store's own key is not the library's, and is fine.
  ExtractFromXgToCsv's `MemoryBrowserStorage`, a fake `IJSRuntime`, still spells
  them, for the simulated storage the plan deliberately is not.
- **Call `Verify`.** An unexpected call fails where it is made, but code under
  test may swallow that failure, and a call that was never made fails
  nowhere else. A test that plans storage ends with `Verify`.
- **Declare successive answers in the order they are given.** Two answers
  for one call — the same operation, area, key and written value — are given
  in declaration order. Calls that differ are never ordered by declaring
  them; say `RequireOrder` when the order is what the test is about.
- **A held call never times out, so bound every wait on one.** Await a held
  operation with `WaitAsync(BunitContext.DefaultWaitTimeout)`, the test's
  normal timeout. Then a call that is never completed fails the test instead
  of hanging it. The deadline is only that guard; the reached, pending and
  completed states are the evidence. A test that leaves a call pending to
  assert on the pending state keeps the operation, and releases and awaits it
  in cleanup.
- **`Release` does not run the awaiting code, and release order is not
  completion order.** The code resumes afterwards, on its own context, and
  two calls released in turn may finish in either order. Before asserting on
  what a completion did, wait for that completion to finish: await the
  caller's operation (the task `ClickAsync` returns, for a click), or an
  observation only that continuation produces. A render showing the expected
  state is not that evidence. The state may hold before the continuation
  runs. And a stale completion that is rightly rejected renders nothing.
  `WaitForAssertion` and `WaitForState` check once at the start and then only
  after each render (bUnit's
  `src/bunit/Extensions/WaitForHelpers/WaitForHelper.cs` at tag `v2.8.6`), so
  they either pass before that continuation has run or wait for a render that
  never comes.

### Changing this library

- **Changing what `Notice` renders** means changing `NoticeBox` and the
  literal boxes in `NoticeBoxTests` in the same commit. A consumer that reads
  through `NoticeBox` changes nothing; one that does not (Consumer tests, above)
  changes with it. A new structural rule in `NoticeBox` needs its failing case
  there too: an assertion never seen failing is unproven.
- **Changing how `BrowserStorage` reaches the browser** — a JavaScript
  module, another argument order, an extra call — means changing
  `BrowserStoragePlan`'s restated wire and `BrowserStorageTests`' literals in
  the same commit. `BrowserStoragePlanTests` fails until the plan agrees with
  the accessor. No consumer on the plan changes; `MemoryBrowserStorage` does.
- **Editing `Notice.razor.css`.** Its selectors are half of the
  inner-content rule and no bUnit test can execute them. After any change,
  check in a real browser that a click on plain text targets the box and a
  click on an inner link or button targets that element.
- **Adding a `NoticeKind` or `NoticeAnnouncement` value** means adding its arm
  to the component's switch — an enum value without one throws at render —
  and its reading to `NoticeBox`, without which
  `NoticeBoxTests.EveryDeclaredKind_AndAnnouncement_IsReadBackFromTheRealComponent`
  fails.
- **Do not put the role on the box, or the close button in the wrapper.**
  Either puts the button's accessible name inside an atomic live region, so
  every dismissible notice would read "Dismiss this message, button" after its
  text.
- **Do not add a handler to the close button.** Its click already reaches the
  box's handler; a second one reports a caller-held dismissal twice.
- **Adding a primitive.** It goes in the root namespace, takes user-level
  selectors, and knows nothing of any domain (Depends on).
