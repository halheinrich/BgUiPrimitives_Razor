using Microsoft.JSInterop;

namespace BgUiPrimitives_Razor.Tests;

// The storage condition's own contract (SPEC-notices.md §2): one occurrence
// per visit, begun by the first refusal; its dismissal held beside it; every
// later refusal the same occurrence; observers told of each change, and of the
// current state when they attach. Carried over from Extract's tests of its own
// holder; the two cases about a throwing logger moved to
// ReportingBrowserStorageTests, since the condition no longer logs. The reload
// row — a fresh holder — can only be staged here.
public class BrowserStorageConditionTests
{
    private static JSException Refusal() => new("SecurityError: The operation is insecure.");

    [Fact]
    public void AFreshHolder_HasNoOccurrence()
    {
        // A reload constructs a fresh holder: everything returns.
        var condition = new BrowserStorageCondition();

        Assert.False(condition.HasOccurred);
        Assert.False(condition.IsNoticeDismissed);
    }

    [Fact]
    public void TheFirstRefusal_BeginsTheOccurrence_AndLaterOnesAreTheSameOccurrence()
    {
        var condition = new BrowserStorageCondition();
        var notified = 0;
        using var _ = condition.Attach(() => notified++);
        notified = 0;

        condition.ReportRefused(Refusal());
        condition.ReportRefused(Refusal());
        condition.ReportRefused(Refusal());

        Assert.True(condition.HasOccurred);
        Assert.Equal(1, notified);
    }

    [Fact]
    public void ADismissal_SurvivesFurtherRefusals()
    {
        var condition = new BrowserStorageCondition();
        condition.ReportRefused(Refusal());

        condition.DismissNotice();
        condition.ReportRefused(Refusal());

        Assert.True(condition.HasOccurred);
        Assert.True(condition.IsNoticeDismissed);
    }

    [Fact]
    public void Dismissing_BeforeAnyRefusal_Throws_AndRecordsNothing()
    {
        var condition = new BrowserStorageCondition();
        var notified = 0;
        using var _ = condition.Attach(() => notified++);
        notified = 0;

        Assert.Throws<InvalidOperationException>(condition.DismissNotice);

        Assert.False(condition.IsNoticeDismissed);
        Assert.False(condition.HasOccurred);
        Assert.Equal(0, notified);
    }

    [Fact]
    public void Attach_DeliversTheCurrentStateBeforeItReturns_ThenEachChange()
    {
        var condition = new BrowserStorageCondition();
        condition.ReportRefused(Refusal());
        var seen = new List<(bool Occurred, bool Dismissed)>();

        using var _ = condition.Attach(() => seen.Add((condition.HasOccurred, condition.IsNoticeDismissed)));
        Assert.Equal([(true, false)], seen);

        condition.DismissNotice();
        condition.DismissNotice(); // already dismissed: no change, no notification

        Assert.Equal([(true, false), (true, true)], seen);
    }

    [Fact]
    public void DisposingTheAttachment_StopsTheObservation()
    {
        var condition = new BrowserStorageCondition();
        var notified = 0;
        var attachment = condition.Attach(() => notified++);
        notified = 0;

        attachment.Dispose();
        attachment.Dispose(); // twice is harmless
        condition.ReportRefused(Refusal());

        Assert.Equal(0, notified);
        Assert.True(condition.HasOccurred);
    }

    // Observers are told from a copy: one that detaches itself while being
    // told changes nothing for the others this time.
    [Fact]
    public void AnObserverDetachingMidNotification_ChangesNothingForTheOthers()
    {
        var condition = new BrowserStorageCondition();
        var first = 0;
        var second = 0;
        IDisposable? firstAttachment = null;
        firstAttachment = condition.Attach(() =>
        {
            first++;
            if (condition.HasOccurred) firstAttachment!.Dispose();
        });
        using var _ = condition.Attach(() => second++);
        first = second = 0;

        condition.ReportRefused(Refusal());
        condition.DismissNotice();

        Assert.Equal(1, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public void ReportRefused_OnNull_Throws_AndRecordsNothing()
    {
        var condition = new BrowserStorageCondition();

        var thrown = Assert.Throws<ArgumentNullException>(() => condition.ReportRefused(null!));

        Assert.Equal("refusal", thrown.ParamName);
        Assert.False(condition.HasOccurred);
    }

    [Fact]
    public void Attach_OnNull_Throws()
    {
        var condition = new BrowserStorageCondition();

        var thrown = Assert.Throws<ArgumentNullException>(() => condition.Attach(null!));

        Assert.Equal("observer", thrown.ParamName);
    }
}
