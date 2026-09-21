using BgUiPrimitives_Razor.TestSupport;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace BgUiPrimitives_Razor.Tests;

// NoticeBox is the reader consumers' tests use instead of naming the
// component's markup, so it is pinned from both sides.
//
// From the markup side, with literal boxes typed here: this file is the one
// place in any test that spells a notice out, and it is what keeps the reader
// an independent statement of the rendered shape rather than an echo of the
// component (NoticeTests then renders the real component through the reader,
// and the two must agree). Literal markup is also the only way to hand the
// reader a box the component would never render.
//
// And every rule is shown failing. An assertion that has only ever run green
// is unproven — the first adopter's splat clause was exactly that — so for
// each rule the reader enforces, a test hands it a box that breaks the rule
// and expects NoticeAssertionException.
public class NoticeBoxTests : BunitContext
{
    // The three shapes, as INSTRUCTIONS.md (Public API, Rendered shape) gives
    // them, with what the renderers stamp: the scope attribute, and the test
    // renderer's handler markers.
    private const string NotDismissible = """
        <div id="n" class="alert alert-warning" b-scope><div class="bg-notice-content" role="status" b-scope>Text</div></div>
        """;

    private const string DismissibleBox = """
        <div id="n" class="alert alert-danger alert-dismissible mb-4" style="max-width:800px" blazor:onclick="1" b-scope><div class="bg-notice-content" role="alert" blazor:onclick:stopPropagation b-scope>Text</div><button type="button" class="btn-close" aria-label="Dismiss this message" b-scope></button></div>
        """;

    private const string Enclosed = """
        <div id="n" class="alert alert-success mt-3 mb-0" b-scope><div class="bg-notice-content" b-scope>Text</div></div>
        """;

    private IRenderedComponent<IComponent> Markup(string html)
        => Render(builder => builder.AddMarkupContent(0, html));

    private NoticeBox Read(string html) => NoticeBox.ById(Markup(html), "n");

    private NoticeAssertionException Refuses(string html)
        => Assert.Throws<NoticeAssertionException>(() => Read(html));

    private IRenderedComponent<Notice> RenderNotice(
        Action<ComponentParameterCollectionBuilder<Notice>> parameters)
        => Render<Notice>(p =>
        {
            p.AddChildContent("Text");
            parameters(p);
        });

    // ---- Reading literal markup ----------------------------------------

    [Theory]
    [InlineData("alert-info", NoticeKind.Information)]
    [InlineData("alert-warning", NoticeKind.Warning)]
    [InlineData("alert-danger", NoticeKind.Error)]
    [InlineData("alert-success", NoticeKind.Success)]
    public void TheKind_IsReadFromTheStylesheetClass(string kindClass, NoticeKind expected)
    {
        var box = Read($"""
            <div id="n" class="alert {kindClass}"><div class="bg-notice-content" role="status">Text</div></div>
            """);

        Assert.Equal(expected, box.Kind);
    }

    // Every declared kind can be read back: a kind added to the enum and the
    // component without its class here fails, rather than failing a consumer.
    [Fact]
    public void EveryDeclaredKind_AndAnnouncement_IsReadBackFromTheRealComponent()
    {
        foreach (var kind in Enum.GetValues<NoticeKind>())
        {
            foreach (var announcement in Enum.GetValues<NoticeAnnouncement>())
            {
                var cut = RenderNotice(p => p
                    .Add(x => x.Kind, kind)
                    .Add(x => x.Announcement, announcement));

                var box = Assert.Single(NoticeBox.AllIn(cut));
                Assert.Equal(kind, box.Kind);
                Assert.Equal(announcement, box.Announcement);
            }
        }
    }

    [Fact]
    public void TheThreeShapes_AreReadInTheComponentsVocabulary()
    {
        var plain = Read(NotDismissible)
            .ShouldBe(NoticeKind.Warning, NoticeAnnouncement.Polite, dismissible: false, "id");
        Assert.Empty(plain.CallerClasses);

        var dismissible = Read(DismissibleBox).ShouldBe(
            NoticeKind.Error, NoticeAnnouncement.Assertive, dismissible: true,
            "id", "class", "style");
        Assert.Equal(["mb-4"], dismissible.CallerClasses);
        Assert.Equal(["id", "class", "style"], dismissible.CallerAttributes);
        Assert.Equal("n", dismissible.Element.Id);
        Assert.Equal("Text", dismissible.Content.TextContent);

        var enclosed = Read(Enclosed).ShouldBe(
            NoticeKind.Success, NoticeAnnouncement.ByEnclosingRegion, dismissible: false,
            "id", "class");
        Assert.Equal(["mt-3", "mb-0"], enclosed.CallerClasses);
    }

    // Caller attribute names are matched as HTML matches them.
    [Fact]
    public void CallerAttributeNames_AreNotCaseSensitive()
    {
        Read(NotDismissible)
            .ShouldBe(NoticeKind.Warning, NoticeAnnouncement.Polite, dismissible: false, "ID");
    }

    // A handler the call site bound is the call site's attribute, named as it
    // was written — never as the test renderer's marker. The component's own
    // dismiss handler is not the caller's and is never named.
    [Fact]
    public void AHandlerTheCallSiteBound_IsNamedAsItWasWritten()
    {
        var cut = RenderNotice(p => p
            .Add(x => x.Kind, NoticeKind.Information)
            .Add(x => x.Dismissible, true)
            .AddUnmatched("onmouseover", EventCallback.Factory.Create(this, () => { })));

        var box = Assert.Single(NoticeBox.AllIn(cut));

        Assert.Equal(["onmouseover"], box.CallerAttributes);
        box.ShouldBe(NoticeKind.Information, NoticeAnnouncement.Polite, dismissible: true,
            "onmouseover");
    }

    // Inner content with a role that is not a live region is the caller's
    // business, as is an inner handler on a notice that is not dismissible.
    [Fact]
    public void OrdinaryInnerContent_IsNotTheReadersConcern()
    {
        Read("""
            <div id="n" class="alert alert-info"><div class="bg-notice-content" role="status"><ul role="list"><li>One</li></ul><button blazor:onclick="3">Retry</button></div></div>
            """).ShouldBe(NoticeKind.Information, NoticeAnnouncement.Polite, dismissible: false, "id");
    }

    // ---- Finding --------------------------------------------------------

    [Fact]
    public void AllIn_ReturnsEveryNotice_InDocumentOrder_AndOnlyNotices()
    {
        var rendered = Markup("""
            <section class="alert-host">
              <div class="alert alert-info"><div class="bg-notice-content" role="status">First</div></div>
              <div class="alert alert-secondary">Hand-written</div>
              <div><div class="alert alert-danger"><div class="bg-notice-content" role="alert">Second</div></div></div>
            </section>
            """);

        var boxes = NoticeBox.AllIn(rendered);

        Assert.Equal(["First", "Second"], boxes.Select(b => b.Content.TextContent));
        Assert.Equal([NoticeKind.Information, NoticeKind.Error], boxes.Select(b => b.Kind));
    }

    [Fact]
    public void AllIn_IsEmpty_WhenNoNoticeIsShown()
    {
        Assert.Empty(NoticeBox.AllIn(Markup("<p>Nothing to say.</p>")));
    }

    [Fact]
    public void AllIn_Fails_OnAMalformedNotice()
    {
        var rendered = Markup("""
            <div class="alert"><div class="bg-notice-content" role="status">Text</div></div>
            """);

        Assert.Throws<NoticeAssertionException>(() => NoticeBox.AllIn(rendered));
    }

    [Fact]
    public void ByMarkerClass_FindsTheCallersNotice()
    {
        var rendered = Markup(NotDismissible + Enclosed.Replace("id=\"n\"", "id=\"m\""));

        Assert.Equal("m", NoticeBox.ByMarkerClass(rendered, "mt-3").Element.Id);
    }

    [Theory]
    [InlineData("missing", "found 0")]
    [InlineData("twice", "found 2")]
    public void ById_Fails_UnlessExactlyOneElementCarriesTheId(string id, string expected)
    {
        var rendered = Markup("""
            <div id="twice" class="alert alert-info"><div class="bg-notice-content">A</div></div>
            <div id="twice" class="alert alert-info"><div class="bg-notice-content">B</div></div>
            """);

        var ex = Assert.Throws<NoticeAssertionException>(() => NoticeBox.ById(rendered, id));

        Assert.Contains(expected, ex.Message);
    }

    [Theory]
    [InlineData("missing", "found 0")]
    [InlineData("marker", "found 2")]
    public void ByMarkerClass_Fails_UnlessExactlyOneElementCarriesTheClass(
        string markerClass, string expected)
    {
        var rendered = Markup("""
            <div class="alert alert-info marker"><div class="bg-notice-content">A</div></div>
            <div class="alert alert-info marker"><div class="bg-notice-content">B</div></div>
            """);

        var ex = Assert.Throws<NoticeAssertionException>(
            () => NoticeBox.ByMarkerClass(rendered, markerClass));

        Assert.Contains(expected, ex.Message);
    }

    // The finder takes the caller's identifiers, never the component's: a
    // test that reaches for one of the component's classes is refused as a
    // misuse, whatever is rendered.
    [Theory]
    [InlineData("alert")]
    [InlineData("alert-info")]
    [InlineData("alert-warning")]
    [InlineData("alert-danger")]
    [InlineData("alert-success")]
    [InlineData("alert-dismissible")]
    [InlineData("bg-notice-content")]
    [InlineData("btn-close")]
    [InlineData("two classes")]
    [InlineData(" ")]
    public void ByMarkerClass_RefusesAComponentClass_AndAnythingNotOneClassName(string markerClass)
    {
        var rendered = Markup(DismissibleBox);

        Assert.ThrowsAny<ArgumentException>(() => NoticeBox.ByMarkerClass(rendered, markerClass));
    }

    // ---- Shown failing: the element is not a notice box ------------------

    [Theory]
    // The id is on the content, not the box.
    [InlineData("""<div class="alert alert-info"><div id="n" class="bg-notice-content" role="status">Text</div></div>""")]
    // The id is on a wrapper around the box.
    [InlineData("""<div id="n"><div class="alert alert-info"><div class="bg-notice-content" role="status">Text</div></div></div>""")]
    // A hand-written alert: the classes, but not the component.
    [InlineData("""<div id="n" class="alert alert-info" role="status">Text</div>""")]
    // Not a div.
    [InlineData("""<section id="n" class="alert alert-info"><div class="bg-notice-content" role="status">Text</div></section>""")]
    // The content is not the box's direct child.
    [InlineData("""<div id="n" class="alert alert-info"><section><div class="bg-notice-content" role="status">Text</div></section></div>""")]
    // Two contents.
    [InlineData("""<div id="n" class="alert alert-info"><div class="bg-notice-content">A</div><div class="bg-notice-content">B</div></div>""")]
    public void From_Fails_OnAnElementThatIsNotANoticeBox(string html)
    {
        var element = Markup(html).Find("#n");

        var ex = Assert.Throws<NoticeAssertionException>(() => NoticeBox.From(element));

        Assert.Contains("not a notice box", ex.Message);
    }

    // ---- Shown failing: the component's classes --------------------------

    [Theory]
    [InlineData("alert", "no kind")]
    [InlineData("alert alert-secondary", "no kind")]
    [InlineData("alert alert-info alert-danger", "more than one kind")]
    // The migration slip: the old classes left in the class the caller passes.
    [InlineData("alert alert-info alert", "'alert'")]
    [InlineData("alert alert-info mb-3 alert-info", "'alert-info'")]
    [InlineData("alert alert-info btn-close", "'btn-close'")]
    public void Reading_Fails_UnlessEachComponentClassAppearsExactlyOnce(
        string classes, string expected)
    {
        var ex = Refuses($"""
            <div id="n" class="{classes}"><div class="bg-notice-content" role="status">Text</div></div>
            """);

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Reading_Fails_WhenTheDismissibleClassIsRepeated()
    {
        var ex = Refuses(DismissibleBox.Replace("mb-4", "alert-dismissible"));

        Assert.Contains("'alert-dismissible'", ex.Message);
    }

    // ---- Shown failing: dismissibility ------------------------------------

    // Dismissible is the class, the close button and the box's click handler
    // together; not dismissible is none of them. Every mixture fails — among
    // them the box that offers no button and yet reacts to a click.
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void Reading_Fails_OnABoxThatIsNeitherDismissibleNorNot(
        bool hasClass, bool hasButton, bool hasHandler)
    {
        var ex = Refuses($"""
            <div id="n" class="alert alert-info{(hasClass ? " alert-dismissible" : "")}"{(hasHandler ? " blazor:onclick=\"1\"" : "")}><div class="bg-notice-content" role="status">Text</div>{(hasButton ? "<button type=\"button\" class=\"btn-close\"></button>" : "")}</div>
            """);

        Assert.Contains("neither dismissible nor not", ex.Message);
    }

    [Fact]
    public void Reading_Fails_WhenTheCloseButtonIsInsideWhatIsAnnounced()
    {
        var ex = Refuses("""
            <div id="n" class="alert alert-info alert-dismissible" blazor:onclick="1"><div class="bg-notice-content" role="status">Text<button type="button" class="btn-close"></button></div></div>
            """);

        Assert.Contains("inside", ex.Message);
    }

    [Fact]
    public void Reading_Fails_OnMoreThanOneCloseButton()
    {
        var ex = Refuses(DismissibleBox.Replace(
            "</button>", "</button><button type=\"button\" class=\"btn-close\"></button>"));

        Assert.Contains("more than one close button", ex.Message);
    }

    // One handler, on the box: the close button's click bubbles to it, and a
    // handler of the button's own would dismiss twice.
    [Fact]
    public void Reading_Fails_WhenTheCloseButtonCarriesAClickHandler()
    {
        var ex = Refuses(DismissibleBox.Replace(
            "class=\"btn-close\"", "class=\"btn-close\" blazor:onclick=\"9\""));

        Assert.Contains("close button carries a click handler", ex.Message);
    }

    // The content reacts to no click of the component's, dismissible or not:
    // not dismissible, nothing may; dismissible, the box's handler is the one.
    [Theory]
    [InlineData(NotDismissible)]
    [InlineData(DismissibleBox)]
    public void Reading_Fails_WhenTheContentCarriesAClickHandler(string html)
    {
        var ex = Refuses(html.Replace(
            "class=\"bg-notice-content\"", "class=\"bg-notice-content\" blazor:onclick=\"9\""));

        Assert.Contains("content carries a click handler", ex.Message);
    }

    // ---- Shown failing: the announcement ----------------------------------

    [Theory]
    [InlineData(NotDismissible)]
    [InlineData(DismissibleBox)]
    [InlineData(Enclosed)]
    public void Reading_Fails_WhenTheBoxItselfCarriesARole(string html)
    {
        var ex = Refuses(html.Replace("id=\"n\"", "id=\"n\" role=\"alert\""));

        Assert.Contains("box itself carries a role", ex.Message);
    }

    // Anywhere: on the box, on the content, inside the content, on the close
    // button — and under every announcement, ByEnclosingRegion above all.
    [Theory]
    [InlineData("""<div id="n" class="alert alert-info" aria-live="polite"><div class="bg-notice-content">Text</div></div>""")]
    [InlineData("""<div id="n" class="alert alert-info" aria-live="off"><div class="bg-notice-content" role="status">Text</div></div>""")]
    [InlineData("""<div id="n" class="alert alert-info"><div class="bg-notice-content" aria-live="assertive">Text</div></div>""")]
    [InlineData("""<div id="n" class="alert alert-info"><div class="bg-notice-content" role="status" aria-atomic="true">Text</div></div>""")]
    [InlineData("""<div id="n" class="alert alert-info"><div class="bg-notice-content"><span aria-relevant="all">Text</span></div></div>""")]
    [InlineData("""<div id="n" class="alert alert-info alert-dismissible" blazor:onclick="1"><div class="bg-notice-content">Text</div><button type="button" class="btn-close" aria-live="polite"></button></div>""")]
    public void Reading_Fails_OnALiveRegionAttributeAnywhereInTheBox(string html)
    {
        var ex = Refuses(html);

        Assert.Contains("no live-region attribute anywhere", ex.Message);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("alert")]
    [InlineData("log")]
    [InlineData("marquee")]
    [InlineData("timer")]
    [InlineData("ALERT")]
    public void Reading_Fails_OnALiveRegionNestedInTheContent(string role)
    {
        var ex = Refuses($"""
            <div id="n" class="alert alert-info"><div class="bg-notice-content"><span role="{role}">Text</span></div></div>
            """);

        Assert.Contains("live region of its own", ex.Message);
    }

    [Theory]
    [InlineData("log")]
    [InlineData("note")]
    [InlineData("")]
    public void Reading_Fails_OnAContentRoleTheComponentDoesNotRender(string role)
    {
        var ex = Refuses($"""
            <div id="n" class="alert alert-info"><div class="bg-notice-content" role="{role}">Text</div></div>
            """);

        Assert.Contains("no announcement the component renders", ex.Message);
    }

    // ---- Shown failing: ShouldBe ------------------------------------------

    [Fact]
    public void ShouldBe_Fails_OnAnotherKind()
    {
        var ex = Assert.Throws<NoticeAssertionException>(() => Read(NotDismissible)
            .ShouldBe(NoticeKind.Error, NoticeAnnouncement.Polite, dismissible: false, "id"));

        Assert.Contains("its kind is Warning, not Error", ex.Message);
    }

    // Each announcement against each other one; the pair that matters most is
    // the one a misspelt parameter produces — asked Assertive, rendered Polite.
    [Fact]
    public void ShouldBe_Fails_OnAnotherAnnouncement()
    {
        foreach (var rendered in Enum.GetValues<NoticeAnnouncement>())
        {
            var box = Assert.Single(NoticeBox.AllIn(RenderNotice(p => p
                .Add(x => x.Kind, NoticeKind.Warning)
                .Add(x => x.Announcement, rendered))));

            foreach (var asked in Enum.GetValues<NoticeAnnouncement>().Where(a => a != rendered))
            {
                var ex = Assert.Throws<NoticeAssertionException>(
                    () => box.ShouldBe(NoticeKind.Warning, asked, dismissible: false));

                Assert.Contains($"its announcement is {rendered}, not {asked}", ex.Message);
            }
        }
    }

    [Theory]
    [InlineData(NotDismissible, true, "it is not dismissible")]
    [InlineData(DismissibleBox, false, "it is dismissible")]
    public void ShouldBe_Fails_OnTheOtherDismissibility(string html, bool asked, string expected)
    {
        var box = Read(html);

        var ex = Assert.Throws<NoticeAssertionException>(() => box.ShouldBe(
            box.Kind, box.Announcement, asked, [.. box.CallerAttributes]));

        Assert.Contains(expected, ex.Message);
    }

    // The silent-splat trap, reproduced through the real component exactly as
    // it was measured in two adopters: the parameter's name is misspelt on the
    // call site, so it is no parameter at all. Nothing throws and nothing
    // warns; the value lands on the box as a plain attribute and the notice is
    // polite. The test says what the call site meant, and both halves of the
    // slip are reported.
    [Fact]
    public void ShouldBe_Fails_OnAMisspeltParameter_SplattedOntoTheBox()
    {
        var cut = RenderNotice(p => p
            .Add(x => x.Kind, NoticeKind.Error)
            .AddUnmatched("id", "runError")
            .AddUnmatched("Anouncement", NoticeAnnouncement.Assertive));

        var ex = Assert.Throws<NoticeAssertionException>(() => NoticeBox.ById(cut, "runError")
            .ShouldBe(NoticeKind.Error, NoticeAnnouncement.Assertive, dismissible: false, "id"));

        Assert.Contains("'anouncement'", ex.Message);
        Assert.Contains("which the test did not name", ex.Message);
        Assert.Contains("its announcement is Polite, not Assertive", ex.Message);
    }

    // The same trap where the misspelt parameter's default happens to be what
    // the test asked for: the stray attribute alone must fail it.
    [Fact]
    public void ShouldBe_Fails_OnAnUnnamedAttribute_EvenWhenEverythingElseAgrees()
    {
        var cut = RenderNotice(p => p
            .Add(x => x.Kind, NoticeKind.Error)
            .AddUnmatched("Announcment", NoticeAnnouncement.Polite));

        var box = Assert.Single(NoticeBox.AllIn(cut));

        var ex = Assert.Throws<NoticeAssertionException>(
            () => box.ShouldBe(NoticeKind.Error, NoticeAnnouncement.Polite, dismissible: false));

        Assert.Contains("'announcment'", ex.Message);
    }

    [Fact]
    public void ShouldBe_Fails_OnANamedAttributeTheBoxDoesNotCarry()
    {
        var ex = Assert.Throws<NoticeAssertionException>(() => Read(NotDismissible).ShouldBe(
            NoticeKind.Warning, NoticeAnnouncement.Polite, dismissible: false, "id", "style"));

        Assert.Contains("does not carry 'style'", ex.Message);
    }

    // "class" is the call site's exactly when it passed one.
    [Fact]
    public void ShouldBe_Fails_WhenACallerClassIsNotNamed_OrNamedButNotPassed()
    {
        var unnamed = Assert.Throws<NoticeAssertionException>(() => Read(Enclosed).ShouldBe(
            NoticeKind.Success, NoticeAnnouncement.ByEnclosingRegion, dismissible: false, "id"));
        Assert.Contains("carries 'class'", unnamed.Message);

        var notPassed = Assert.Throws<NoticeAssertionException>(() => Read(NotDismissible).ShouldBe(
            NoticeKind.Warning, NoticeAnnouncement.Polite, dismissible: false, "id", "class"));
        Assert.Contains("does not carry 'class'", notPassed.Message);
    }

    [Fact]
    public void ShouldBe_StatesEveryDifferenceAtOnce()
    {
        var ex = Assert.Throws<NoticeAssertionException>(() => Read(DismissibleBox).ShouldBe(
            NoticeKind.Warning, NoticeAnnouncement.Polite, dismissible: false, "id", "title"));

        Assert.Contains("its kind is Error, not Warning", ex.Message);
        Assert.Contains("its announcement is Assertive, not Polite", ex.Message);
        Assert.Contains("it is dismissible", ex.Message);
        Assert.Contains("'class', 'style'", ex.Message);
        Assert.Contains("does not carry 'title'", ex.Message);
        Assert.Contains("Rendered: <div", ex.Message);
    }

    // ---- Shown failing: the gestures ---------------------------------------

    [Theory]
    [InlineData(NoticeDismissGesture.CloseButton)]
    [InlineData(NoticeDismissGesture.WholeBox)]
    public void Dismiss_Fails_OnANoticeThatIsNotDismissible(NoticeDismissGesture gesture)
    {
        var cut = RenderNotice(p => p.Add(x => x.Kind, NoticeKind.Warning));
        var box = Assert.Single(NoticeBox.AllIn(cut));

        var ex = Assert.Throws<NoticeAssertionException>(() => box.Dismiss(gesture));

        Assert.Contains("not dismissible", ex.Message);
        Assert.Single(NoticeBox.AllIn(cut));
    }

    [Fact]
    public void Dismiss_RefusesAnUndeclaredGesture()
    {
        var cut = RenderNotice(p => p
            .Add(x => x.Kind, NoticeKind.Warning)
            .Add(x => x.Dismissible, true));
        var box = Assert.Single(NoticeBox.AllIn(cut));

        Assert.Throws<ArgumentOutOfRangeException>(() => box.Dismiss((NoticeDismissGesture)(-1)));
    }
}
