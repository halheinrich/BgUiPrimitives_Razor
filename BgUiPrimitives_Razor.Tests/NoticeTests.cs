using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace BgUiPrimitives_Razor.Tests;

// Pins for the rules Notice implements from the umbrella's SPEC-notices.md:
// section 3 (how a notice presents) and section 2 (what a dismissal is and
// what it survives), plus the fail-loud guards on the parameter combinations
// that would otherwise let the screen and the dismissal's owner disagree.
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

    private static IElement Box(IRenderedComponent<Notice> cut) => cut.Find("div.alert");

    private static IElement Wrapper(IRenderedComponent<Notice> cut)
        => cut.Find("div.alert > div.bg-notice-content");

    private static bool IsShown<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
        => cut.FindAll("div.alert").Count == 1;

    // ---- Kind → colour -------------------------------------------------

    // The whole class list, not a Contains: with no caller class the box
    // carries the component's classes and nothing else.
    [Theory]
    [InlineData(NoticeKind.Information, "alert alert-info")]
    [InlineData(NoticeKind.Warning, "alert alert-warning")]
    [InlineData(NoticeKind.Error, "alert alert-danger")]
    [InlineData(NoticeKind.Success, "alert alert-success")]
    public void Kind_DecidesTheStylesheetClass(NoticeKind kind, string expectedClass)
    {
        var cut = RenderNotice(kind);

        Assert.Equal(expectedClass, Box(cut).GetAttribute("class"));
    }

    // Every declared kind has a class: a kind added to the enum without one
    // fails here rather than throwing in a consumer's page.
    [Fact]
    public void EveryDeclaredKind_Renders()
    {
        foreach (var kind in Enum.GetValues<NoticeKind>())
        {
            Assert.True(IsShown(RenderNotice(kind)));
        }
    }

    [Fact]
    public void UndeclaredKind_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => RenderNotice((NoticeKind)(-1)));
    }

    // ---- Announcement → the live region ---------------------------------

    [Fact]
    public void Announcement_DefaultsToPolite_RoleStatus()
    {
        var cut = RenderNotice(NoticeKind.Error);

        Assert.Equal("status", Wrapper(cut).GetAttribute("role"));
    }

    // Announcement is not colour: the same kind takes either role.
    [Theory]
    [InlineData(NoticeAnnouncement.Polite, "status")]
    [InlineData(NoticeAnnouncement.Assertive, "alert")]
    public void Announcement_DecidesTheRole_IndependentOfKind(
        NoticeAnnouncement announcement, string expectedRole)
    {
        foreach (var kind in Enum.GetValues<NoticeKind>())
        {
            var cut = RenderNotice(kind, more: p => p.Add(x => x.Announcement, announcement));

            Assert.Equal(expectedRole, Wrapper(cut).GetAttribute("role"));
        }
    }

    // A status or alert region is implicitly atomic, so whatever is inside it
    // is presented with the message. The region is therefore the content
    // wrapper and nothing else: the box carries no role, and the close button
    // is the region's sibling, never its descendant — for both shapes of box.
    [Theory]
    [InlineData(NoticeAnnouncement.Polite, false)]
    [InlineData(NoticeAnnouncement.Polite, true)]
    [InlineData(NoticeAnnouncement.Assertive, false)]
    [InlineData(NoticeAnnouncement.Assertive, true)]
    public void TheLiveRegion_IsTheContentWrapper_AndHoldsOnlyTheContent(
        NoticeAnnouncement announcement, bool dismissible)
    {
        var cut = RenderNotice(dismissible: dismissible,
            more: p => p.Add(x => x.Announcement, announcement));

        var region = Assert.Single(cut.FindAll("[role]"));
        Assert.True(region.Matches("div.alert > div.bg-notice-content"));
        Assert.False(Box(cut).HasAttribute("role"));
        Assert.Equal(Text, region.TextContent);
        Assert.Empty(region.QuerySelectorAll("button"));
        Assert.Equal(dismissible ? 1 : 0, cut.FindAll("div.alert > button.btn-close").Count);
    }

    // Inside a region the caller keeps mounted, the notice must add no region
    // of its own: no live-region role and no live-region attribute, on any
    // element, in either shape of box — and the rest of the box is unchanged.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnouncedByAnEnclosingRegion_RendersNoLiveRegionOfItsOwn(bool dismissible)
    {
        var cut = RenderNotice(NoticeKind.Success, dismissible,
            p => p.Add(x => x.Announcement, NoticeAnnouncement.ByEnclosingRegion));

        Assert.Empty(cut.FindAll("[role], [aria-live], [aria-atomic], [aria-relevant]"));
        Assert.Equal(Text, Wrapper(cut).TextContent);
        Assert.Equal(dismissible ? 1 : 0, cut.FindAll("div.alert > button.btn-close").Count);
    }

    // The shape the value exists for: the caller's standing region is the only
    // live region in the tree, and the box is inside it.
    [Fact]
    public void AnnouncedByAnEnclosingRegion_TheCallersRegionIsTheOnlyOne()
    {
        var cut = Render<StandingRegionHost>();

        var region = Assert.Single(cut.FindAll("[role]"));
        Assert.Equal("loadedNotice", region.Id);
        Assert.Equal("status", region.GetAttribute("role"));
        Assert.NotNull(region.QuerySelector("div.alert.alert-success"));
    }

    // Every declared value renders: a value added to the enum without an arm
    // fails here rather than throwing in a consumer's page.
    [Fact]
    public void EveryDeclaredAnnouncement_Renders()
    {
        foreach (var announcement in Enum.GetValues<NoticeAnnouncement>())
        {
            Assert.True(IsShown(RenderNotice(more: p => p.Add(x => x.Announcement, announcement))));
        }
    }

    [Fact]
    public void UndeclaredAnnouncement_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => RenderNotice(more: p => p.Add(x => x.Announcement, (NoticeAnnouncement)(-1))));
    }

    // ---- Dismissibility ------------------------------------------------

    // The default is the safe one: a caller that says nothing gets a box the
    // user cannot close.
    [Fact]
    public void ByDefault_TheNoticeIsNotDismissible()
    {
        var cut = Render<Notice>(parameters => parameters
            .Add(p => p.Kind, NoticeKind.Warning)
            .AddChildContent(Text));

        Assert.Empty(cut.FindAll("button"));
        Assert.DoesNotContain("alert-dismissible", Box(cut).ClassList);
    }

    // "Ignores a click" is pinned at its strongest: the box carries no click
    // handler at all, so there is nothing for a click to reach.
    [Fact]
    public void NonDismissible_RendersNoCloseButton_AndCarriesNoClickHandler()
    {
        var cut = RenderNotice(NoticeKind.Warning, dismissible: false);

        Assert.Empty(cut.FindAll("button"));
        Assert.Throws<MissingEventHandlerException>(() => Box(cut).Click());
        Assert.Throws<MissingEventHandlerException>(
            () => cut.Find(".bg-notice-content").Click());
        Assert.Contains(Text, Box(cut).TextContent);
    }

    [Fact]
    public void Dismissible_RendersTheCloseButton_WithItsSemantics()
    {
        var cut = RenderNotice(NoticeKind.Warning, dismissible: true);

        Assert.Equal("alert alert-warning alert-dismissible", Box(cut).GetAttribute("class"));
        var button = cut.Find("div.alert > button.btn-close");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal("Dismiss this message", button.GetAttribute("aria-label"));
    }

    // The content wrapper is present either way, so a caller's selector does
    // not change when Dismissible does.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Content_RendersInsideTheWrapper(bool dismissible)
    {
        var cut = RenderNotice(dismissible: dismissible);

        Assert.Equal(Text, cut.Find("div.alert > div.bg-notice-content").TextContent);
    }

    // ---- The two gestures ----------------------------------------------

    [Fact]
    public void ClickingTheBox_Dismisses()
    {
        var cut = RenderNotice(dismissible: true);

        Box(cut).Click();

        Assert.False(IsShown(cut));
        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    [Fact]
    public void ClickingTheCloseButton_Dismisses()
    {
        var cut = RenderNotice(dismissible: true);

        cut.Find("button.btn-close").Click();

        Assert.False(IsShown(cut));
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
    // on both the box and the wrapper.
    [Fact]
    public void TheBoxAndTheWrapper_CarryTheScopedStylesheetsAttribute()
    {
        var cut = RenderNotice(dismissible: true);

        var boxScope = Assert.Single(Box(cut).Attributes, a => a.Name.StartsWith("b-"));
        var wrapperScope = Assert.Single(
            cut.Find(".bg-notice-content").Attributes, a => a.Name.StartsWith("b-"));
        Assert.Equal(boxScope.Name, wrapperScope.Name);
    }

    // ---- Identity and caller attributes --------------------------------

    [Fact]
    public void IdAndOtherAttributes_LandOnTheBoxItself()
    {
        var cut = RenderNotice(NoticeKind.Warning, dismissible: true, more: p => p
            .AddUnmatched("id", "truncationNotice")
            .AddUnmatched("style", "white-space: pre-line")
            .AddUnmatched("data-occurrence", "7"));

        // Found by id *as* the box: the id is on the alert element, not on a
        // wrapper around it or inside it.
        var box = cut.Find("div.alert#truncationNotice");
        Assert.Equal("white-space: pre-line", box.GetAttribute("style"));
        Assert.Equal("7", box.GetAttribute("data-occurrence"));
    }

    // Merged after the component's classes, never replacing them — for both
    // shapes of box.
    [Theory]
    [InlineData(false, "alert alert-warning skipped-notice mt-2")]
    [InlineData(true, "alert alert-warning alert-dismissible skipped-notice mt-2")]
    public void ACallersClass_IsMergedWithTheComponentsOwn(bool dismissible, string expected)
    {
        var cut = RenderNotice(NoticeKind.Warning, dismissible, p => p
            .AddUnmatched("class", "  skipped-notice mt-2 "));

        Assert.Equal(expected, Box(cut).GetAttribute("class"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankCallerClass_AddsNothing(string blank)
    {
        var cut = RenderNotice(NoticeKind.Success, more: p => p.AddUnmatched("class", blank));

        Assert.Equal("alert alert-success", Box(cut).GetAttribute("class"));
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

        Assert.Equal("hint", Box(cut).GetAttribute("aria-describedby"));
    }

    // ---- Dismissal held by the component -------------------------------

    // The caller writes nothing, and a re-render of the same occurrence does
    // not resurrect the notice.
    [Fact]
    public void ComponentHeld_StaysDismissed_AcrossReRenders()
    {
        var cut = RenderNotice(dismissible: true);
        Box(cut).Click();

        cut.Render(p => p.Add(x => x.Kind, NoticeKind.Warning));

        Assert.False(IsShown(cut));
    }

    [Fact]
    public void ComponentHeld_StaysDismissed_WhileTheOccurrenceKeyIsEqual()
    {
        // Equal but not the same reference: the comparison is Equals.
        var cut = RenderNotice(dismissible: true,
            more: p => p.Add(x => x.OccurrenceKey, new string('k', 3)));
        Box(cut).Click();

        cut.Render(p => p.Add(x => x.OccurrenceKey, new string('k', 3)));

        Assert.False(IsShown(cut));
    }

    [Fact]
    public void ComponentHeld_ANewOccurrenceKey_ShowsFresh_AndCanBeDismissedAgain()
    {
        var cut = RenderNotice(dismissible: true, more: p => p.Add(x => x.OccurrenceKey, 1));
        Box(cut).Click();

        cut.Render(p => p.Add(x => x.OccurrenceKey, 2));
        Assert.True(IsShown(cut));

        Box(cut).Click();
        Assert.False(IsShown(cut));
    }

    // Null is a key like any other, so a caller whose occurrence token starts
    // out null gets a fresh notice when the first real one arrives.
    [Fact]
    public void ComponentHeld_AKeyArrivingAfterNone_ShowsFresh()
    {
        var cut = RenderNotice(dismissible: true);
        Box(cut).Click();

        cut.Render(p => p.Add(x => x.OccurrenceKey, "first"));

        Assert.True(IsShown(cut));
    }

    // ---- Dismissal held by the caller ----------------------------------

    [Theory]
    [InlineData("div.alert")]
    [InlineData("button.btn-close")]
    public void CallerHeld_EitherGesture_ReportsTrueExactlyOnce(string target)
    {
        var owner = new DismissalOwner();
        var cut = Render<OwnerHost>(p => p.Add(h => h.Owner, owner));

        cut.Find(target).Click();

        Assert.Equal([true], owner.Reports);
        Assert.False(IsShown(cut));
    }

    // The owner's value wins: an owner that declines the report keeps its
    // notice, because the component holds no bit of its own to hide it with.
    // The box surviving the gesture is also what makes a double report
    // visible: the close button's click travels the box's one handler, and a
    // second handler on the button would report twice here.
    [Theory]
    [InlineData("div.alert")]
    [InlineData("button.btn-close")]
    public void CallerHeld_AnOwnerThatDeclines_KeepsTheNoticeShown(string target)
    {
        var owner = new DismissalOwner { AcceptsReports = false };
        var cut = Render<OwnerHost>(p => p.Add(h => h.Owner, owner));

        cut.Find(target).Click();

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
        firstVisit.Find("button.btn-close").Click();

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
        cut.Find("div.alert").Click();
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
