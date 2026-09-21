using BgUiPrimitives_Razor.TestSupport;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace BgUiPrimitives_Razor.Tests;

// Pins for the rules Notice implements from the umbrella's SPEC-notices.md:
// section 3 (how a notice presents) and section 2 (what a dismissal is and
// what it survives), plus the fail-loud guards on the parameter combinations
// that would otherwise let the screen and the dismissal's owner disagree.
//
// What a notice looks like in markup is read through NoticeBox, the reader
// consumers' tests use: these tests say what the notice is, in the
// component's vocabulary, and the reader — pinned against literal markup by
// NoticeBoxTests — owns the classes, the roles and where they sit. What the
// reader does not cover (the close button's own semantics, the scope
// attribute) is still read here directly.
public class NoticeTests : BunitContext
{
    private const string Text = "Something happened.";

    private IRenderedComponent<Notice> RenderNotice(
        NoticeKind kind = NoticeKind.Information,
        bool dismissible = false,
        Action<ComponentParameterCollectionBuilder<Notice>>? more = null)
        => Render<Notice>(parameters =>
        {
            parameters
                .Add(p => p.Kind, kind)
                .Add(p => p.Dismissible, dismissible)
                .AddChildContent(Text);
            more?.Invoke(parameters);
        });

    private static NoticeBox Box(IRenderedComponent<IComponent> cut)
        => Assert.Single(NoticeBox.AllIn(cut));

    private static bool IsShown(IRenderedComponent<IComponent> cut)
        => NoticeBox.AllIn(cut).Count == 1;

    public static TheoryData<NoticeDismissGesture> BothGestures =>
        new(Enum.GetValues<NoticeDismissGesture>());

    // ---- What the caller names is what renders -------------------------

    // The three selectors are independent — announcement is not colour, and
    // neither decides dismissibility — so every combination renders as asked,
    // in a well-formed box: the announcement on the content and nowhere else,
    // the close button (when there is one) outside it, and nothing on the box
    // or in its class list that the caller did not pass. With
    // ByEnclosingRegion that is no live-region role or attribute anywhere.
    [Fact]
    public void EveryCombination_RendersAsAsked_WithTheContentInside()
    {
        foreach (var kind in Enum.GetValues<NoticeKind>())
        {
            foreach (var announcement in Enum.GetValues<NoticeAnnouncement>())
            {
                foreach (var dismissible in new[] { false, true })
                {
                    var cut = RenderNotice(kind, dismissible,
                        p => p.Add(x => x.Announcement, announcement));

                    var box = Box(cut).ShouldBe(kind, announcement, dismissible);
                    Assert.Empty(box.CallerClasses);
                    Assert.Equal(Text, box.Content.TextContent);
                    Assert.Equal(Text, box.Element.TextContent);
                }
            }
        }
    }

    [Fact]
    public void UndeclaredKind_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => RenderNotice((NoticeKind)(-1)));
    }

    [Fact]
    public void UndeclaredAnnouncement_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => RenderNotice(more: p => p.Add(x => x.Announcement, (NoticeAnnouncement)(-1))));
    }

    // The defaults are the safe ones: a caller that names only the kind gets
    // a polite box the user cannot close.
    [Fact]
    public void ByDefault_TheNoticeIsPolite_AndNotDismissible()
    {
        var cut = Render<Notice>(parameters => parameters
            .Add(p => p.Kind, NoticeKind.Warning)
            .AddChildContent(Text));

        Box(cut).ShouldBe(NoticeKind.Warning, NoticeAnnouncement.Polite, dismissible: false);
    }

    // The shape ByEnclosingRegion exists for: the caller's standing region is
    // the only live region in the tree, and the box is inside it.
    [Fact]
    public void AnnouncedByAnEnclosingRegion_TheCallersRegionIsTheOnlyOne()
    {
        var cut = Render<StandingRegionHost>();

        var region = Assert.Single(cut.FindAll("[role]"));
        Assert.Equal("loadedNotice", region.Id);
        Assert.Equal("status", region.GetAttribute("role"));

        var box = Box(cut).ShouldBe(
            NoticeKind.Success, NoticeAnnouncement.ByEnclosingRegion, dismissible: false);
        Assert.True(region.Contains(box.Element));
    }

    // ---- Dismissibility ------------------------------------------------

    // "Ignores a click" is pinned at its strongest, and behaviourally: a
    // click on the box or on its content reaches no handler at all. This is
    // also what stands behind the reader's rule, which reads the same fact
    // off the test renderer's handler markers without clicking anything.
    [Fact]
    public void NonDismissible_ReactsToNoClick()
    {
        var cut = RenderNotice(NoticeKind.Warning, dismissible: false);
        var box = Box(cut);

        Assert.Throws<MissingEventHandlerException>(() => box.Element.Click());
        Assert.Throws<MissingEventHandlerException>(() => box.Content.Click());
        Assert.True(IsShown(cut));
    }

    [Fact]
    public void Dismissible_TheCloseButtonCarriesItsSemantics()
    {
        var cut = RenderNotice(NoticeKind.Warning, dismissible: true);

        var button = Assert.Single(cut.FindAll("button"));
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal("Dismiss this message", button.GetAttribute("aria-label"));
    }

    // ---- The two gestures ----------------------------------------------

    [Theory]
    [MemberData(nameof(BothGestures))]
    public void EitherGesture_Dismisses_AndTheNoticeRendersNothing(NoticeDismissGesture gesture)
    {
        var cut = RenderNotice(dismissible: true);

        Box(cut).Dismiss(gesture);

        Assert.False(IsShown(cut));
        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    // ---- Inner interactive content does not dismiss --------------------

    // The caller's button does its own job and the box stays, with no
    // stop-propagation attribute written by the caller.
    [Fact]
    public void AnInnerButton_RunsItsOwnHandler_AndDoesNotDismiss()
    {
        var innerClicks = 0;
        var cut = Render<Notice>(parameters => parameters
            .Add(p => p.Kind, NoticeKind.Warning)
            .Add(p => p.Dismissible, true)
            .Add(p => p.ChildContent, builder =>
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, "id", "escape");
                builder.AddAttribute(2, "onclick",
                    EventCallback.Factory.Create(this, () => innerClicks++));
                builder.AddContent(3, "Start without mix");
                builder.CloseElement();
            }));

        cut.Find("#escape").Click();

        Assert.Equal(1, innerClicks);
        Assert.True(IsShown(cut));
    }

    // A plain link has no Blazor handler of its own, and its click reaches
    // none: the dismiss handler on the box is out of its reach.
    [Fact]
    public void AnInnerLink_ReachesNoHandler_AndDoesNotDismiss()
    {
        var cut = Render<Notice>(parameters => parameters
            .Add(p => p.Kind, NoticeKind.Information)
            .Add(p => p.Dismissible, true)
            .AddChildContent("<a id=\"help\" href=\"help\">Read more</a>"));

        Assert.Throws<MissingEventHandlerException>(() => cut.Find("#help").Click());
        Assert.True(IsShown(cut));
    }

    // The large target's other half lives in Notice.razor.css, which no bUnit
    // test can execute. What can be pinned is that the stylesheet is attached:
    // the compiler stamps a scope attribute on the component's elements only
    // while Notice.razor.css exists, and the stylesheet's selectors need it
    // on both the box and the content.
    [Fact]
    public void TheBoxAndItsContent_CarryTheScopedStylesheetsAttribute()
    {
        var box = Box(RenderNotice(dismissible: true));

        var boxScope = Assert.Single(box.Element.Attributes, a => a.Name.StartsWith("b-"));
        var contentScope = Assert.Single(box.Content.Attributes, a => a.Name.StartsWith("b-"));
        Assert.Equal(boxScope.Name, contentScope.Name);
    }

    // ---- Identity and caller attributes --------------------------------

    // Found by id *as* the box: the id is on the notice box itself, not on a
    // wrapper around it or inside it — and the box carries what was passed
    // and nothing else.
    [Fact]
    public void IdAndOtherAttributes_LandOnTheBoxItself()
    {
        var cut = RenderNotice(NoticeKind.Warning, dismissible: true, more: p => p
            .AddUnmatched("id", "truncationNotice")
            .AddUnmatched("style", "white-space: pre-line")
            .AddUnmatched("data-occurrence", "7"));

        var box = NoticeBox.ById(cut, "truncationNotice").ShouldBe(
            NoticeKind.Warning, NoticeAnnouncement.Polite, dismissible: true,
            "id", "style", "data-occurrence");
        Assert.Equal("white-space: pre-line", box.Element.GetAttribute("style"));
        Assert.Equal("7", box.Element.GetAttribute("data-occurrence"));
    }

    // Merged with the component's classes, never replacing them — for both
    // shapes of box — and the notice is findable by the caller's marker.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACallersClass_IsMergedWithTheComponentsOwn(bool dismissible)
    {
        var cut = RenderNotice(NoticeKind.Warning, dismissible, p => p
            .AddUnmatched("class", "  skipped-notice mt-2 "));

        var box = NoticeBox.ByMarkerClass(cut, "skipped-notice").ShouldBe(
            NoticeKind.Warning, NoticeAnnouncement.Polite, dismissible, "class");
        Assert.Equal(["skipped-notice", "mt-2"], box.CallerClasses);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankCallerClass_AddsNothing(string blank)
    {
        var cut = RenderNotice(NoticeKind.Success, more: p => p.AddUnmatched("class", blank));

        var box = Box(cut).ShouldBe(
            NoticeKind.Success, NoticeAnnouncement.Polite, dismissible: false);
        Assert.Empty(box.CallerClasses);
    }

    // The splat guard. A click handler left on the tag by a migrating caller
    // would lose to the component's without a word; a role or live-region
    // attribute would land on the box and make it a second live region, which
    // is Announcement's decision and nobody else's.
    [Theory]
    [InlineData("role")]
    [InlineData("ROLE")]
    [InlineData("onclick")]
    [InlineData("aria-live")]
    [InlineData("Aria-Live")]
    [InlineData("aria-atomic")]
    [InlineData("aria-relevant")]
    public void AnAttributeTheComponentOwns_IsRefused(string name)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => RenderNotice(more: p => p.AddUnmatched(name, "alert")));

        Assert.Contains(name, ex.Message);
    }

    // Refused whatever the announcement: there is no value of Announcement a
    // caller's live-region attribute agrees with. Under ByEnclosingRegion it
    // is the rejected aria-live="off" workaround by another door.
    [Fact]
    public void ALiveRegionAttribute_IsRefused_UnderEveryAnnouncement()
    {
        foreach (var announcement in Enum.GetValues<NoticeAnnouncement>())
        {
            foreach (var name in new[] { "role", "aria-live", "aria-atomic", "aria-relevant" })
            {
                Assert.Throws<InvalidOperationException>(() => RenderNotice(more: p => p
                    .Add(x => x.Announcement, announcement)
                    .AddUnmatched(name, "off")));
            }
        }
    }

    // What the splat is still for: an aria attribute that is not a
    // live-region one passes through to the box like any other.
    [Fact]
    public void AnOrdinaryAriaAttribute_StillLandsOnTheBox()
    {
        var cut = RenderNotice(more: p => p.AddUnmatched("aria-describedby", "hint"));

        var box = Box(cut).ShouldBe(
            NoticeKind.Information, NoticeAnnouncement.Polite, dismissible: false,
            "aria-describedby");
        Assert.Equal("hint", box.Element.GetAttribute("aria-describedby"));
    }

    // ---- Dismissal held by the component -------------------------------

    // The caller writes nothing, and a re-render of the same occurrence does
    // not resurrect the notice.
    [Fact]
    public void ComponentHeld_StaysDismissed_AcrossReRenders()
    {
        var cut = RenderNotice(dismissible: true);
        Box(cut).Dismiss(NoticeDismissGesture.WholeBox);

        cut.Render(p => p.Add(x => x.Kind, NoticeKind.Warning));

        Assert.False(IsShown(cut));
    }

    [Fact]
    public void ComponentHeld_StaysDismissed_WhileTheOccurrenceKeyIsEqual()
    {
        // Equal but not the same reference: the comparison is Equals.
        var cut = RenderNotice(dismissible: true,
            more: p => p.Add(x => x.OccurrenceKey, new string('k', 3)));
        Box(cut).Dismiss(NoticeDismissGesture.WholeBox);

        cut.Render(p => p.Add(x => x.OccurrenceKey, new string('k', 3)));

        Assert.False(IsShown(cut));
    }

    [Fact]
    public void ComponentHeld_ANewOccurrenceKey_ShowsFresh_AndCanBeDismissedAgain()
    {
        var cut = RenderNotice(dismissible: true, more: p => p.Add(x => x.OccurrenceKey, 1));
        Box(cut).Dismiss(NoticeDismissGesture.WholeBox);

        cut.Render(p => p.Add(x => x.OccurrenceKey, 2));
        Assert.True(IsShown(cut));

        Box(cut).Dismiss(NoticeDismissGesture.WholeBox);
        Assert.False(IsShown(cut));
    }

    // Null is a key like any other, so a caller whose occurrence token starts
    // out null gets a fresh notice when the first real one arrives.
    [Fact]
    public void ComponentHeld_AKeyArrivingAfterNone_ShowsFresh()
    {
        var cut = RenderNotice(dismissible: true);
        Box(cut).Dismiss(NoticeDismissGesture.WholeBox);

        cut.Render(p => p.Add(x => x.OccurrenceKey, "first"));

        Assert.True(IsShown(cut));
    }

    // ---- Dismissal held by the caller ----------------------------------

    [Theory]
    [MemberData(nameof(BothGestures))]
    public void CallerHeld_EitherGesture_ReportsTrueExactlyOnce(NoticeDismissGesture gesture)
    {
        var owner = new DismissalOwner();
        var cut = Render<OwnerHost>(p => p.Add(h => h.Owner, owner));

        Box(cut).Dismiss(gesture);

        Assert.Equal([true], owner.Reports);
        Assert.False(IsShown(cut));
    }

    // The owner's value wins: an owner that declines the report keeps its
    // notice, because the component holds no bit of its own to hide it with.
    // The box surviving the gesture is also what makes a double report
    // visible: the close button's click travels the box's one handler, and a
    // second handler on the button would report twice here.
    [Theory]
    [MemberData(nameof(BothGestures))]
    public void CallerHeld_AnOwnerThatDeclines_KeepsTheNoticeShown(NoticeDismissGesture gesture)
    {
        var owner = new DismissalOwner { AcceptsReports = false };
        var cut = Render<OwnerHost>(p => p.Add(h => h.Owner, owner));

        Box(cut).Dismiss(gesture);

        Assert.Equal([true], owner.Reports);
        Assert.True(IsShown(cut));
    }

    // Navigating away and back: the page and its Notice are new, the owner is
    // not, and the owner's dismissal is what the new page renders from.
    [Fact]
    public void CallerHeld_ADismissalSurvivesANewComponentInstance()
    {
        var owner = new DismissalOwner();
        var firstVisit = Render<OwnerHost>(p => p.Add(h => h.Owner, owner));
        Box(firstVisit).Dismiss(NoticeDismissGesture.CloseButton);

        var secondVisit = Render<OwnerHost>(p => p.Add(h => h.Owner, owner));

        Assert.False(IsShown(secondVisit));
    }

    // A new occurrence is the owner's to declare, and the same component
    // instance shows it: nothing in the component remembers the old one.
    [Fact]
    public void CallerHeld_WhenTheOwnerStartsANewOccurrence_TheNoticeShowsFresh()
    {
        var owner = new DismissalOwner();
        var cut = Render<OwnerHost>(p => p.Add(h => h.Owner, owner));
        Box(cut).Dismiss(NoticeDismissGesture.WholeBox);
        Assert.False(IsShown(cut));

        owner.IsDismissed = false;
        cut.Render();

        Assert.True(IsShown(cut));
    }

    // A gate reason must never be hidden by a dismissal, however it arrives.
    [Fact]
    public void NonDismissible_IgnoresABoundDismissedState()
    {
        var cut = RenderNotice(NoticeKind.Warning, dismissible: false,
            more: p => p.Bind(x => x.Dismissed, true, _ => { }));

        Assert.True(IsShown(cut));
    }

    // ---- One holder, never two -----------------------------------------

    [Fact]
    public void DismissedWithoutDismissedChanged_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => RenderNotice(dismissible: true,
            more: p => p.Add(x => x.Dismissed, false)));
    }

    [Fact]
    public void DismissedChangedWithoutDismissed_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => RenderNotice(dismissible: true,
            more: p => p.Add(x => x.DismissedChanged, (bool _) => { })));
    }

    [Fact]
    public void ABoundDismissed_TogetherWithAnOccurrenceKey_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => RenderNotice(dismissible: true,
            more: p => p
                .Bind(x => x.Dismissed, false, _ => { })
                .Add(x => x.OccurrenceKey, 1)));
    }

    // The state an occurrence's owner holds when it outlives the page.
    private sealed class DismissalOwner
    {
        public bool IsDismissed { get; set; }

        public bool AcceptsReports { get; init; } = true;

        public List<bool> Reports { get; } = [];

        public void Report(bool dismissed)
        {
            Reports.Add(dismissed);
            if (AcceptsReports)
            {
                IsDismissed = dismissed;
            }
        }
    }

    // The first adopter's shape: a polite region the caller keeps mounted so
    // that its fills are announced reliably, with the notice as its content.
    private sealed class StandingRegionHost : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "id", "loadedNotice");
            builder.AddAttribute(2, "role", "status");
            builder.OpenComponent<Notice>(3);
            builder.AddComponentParameter(4, nameof(Notice.Kind), NoticeKind.Success);
            builder.AddComponentParameter(5, nameof(Notice.Announcement),
                NoticeAnnouncement.ByEnclosingRegion);
            builder.AddComponentParameter(6, nameof(Notice.ChildContent),
                (RenderFragment)(content => content.AddContent(0, Text)));
            builder.CloseComponent();
            builder.CloseElement();
        }
    }

    // A real parent, so the bound path is exercised over the real wire:
    // gesture → DismissedChanged → the owner → the parent's re-render → the
    // Dismissed parameter. What @bind-Dismissed:get / :set compiles to.
    private sealed class OwnerHost : ComponentBase
    {
        [Parameter, EditorRequired]
        public DismissalOwner Owner { get; set; } = null!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<Notice>(0);
            builder.AddComponentParameter(1, nameof(Notice.Kind), NoticeKind.Information);
            builder.AddComponentParameter(2, nameof(Notice.Dismissible), true);
            builder.AddComponentParameter(3, nameof(Notice.Dismissed), Owner.IsDismissed);
            builder.AddComponentParameter(4, nameof(Notice.DismissedChanged),
                EventCallback.Factory.Create<bool>(this, Owner.Report));
            builder.AddComponentParameter(5, nameof(Notice.ChildContent),
                (RenderFragment)(content => content.AddContent(0, Text)));
            builder.CloseComponent();
        }
    }
}
