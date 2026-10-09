using BgUiPrimitives_Razor.TestSupport;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace BgUiPrimitives_Razor.Tests;

// Pins for BrowserStoragePlan, the planner consumers' tests use in place of
// spelling BrowserStorage's interop calls (halheinrich/backgammon#377).
//
// Every case runs the real BrowserStorage, resolved through the registration
// a host makes, over bUnit's runtime with the plan on it: a planner whose
// answers the real accessor did not read as the plan said would fail here.
// The rule each test pins is named above it.
//
// The plan's identifiers are not the oracle for BrowserStorageTests, and are
// not read here either: the few tests below that must reach the runtime
// without the accessor — a competing handler, a call the accessor never
// makes, an unrelated call — spell what they send for themselves.
public class BrowserStoragePlanTests : BunitContext
{
    private const string Key = "bg.test.key";
    private const string OtherKey = "bg.test.other";
    private const string Value = "stored value";
    private const string OtherValue = "other value";

    private readonly BrowserStoragePlan _plan;

    public BrowserStoragePlanTests()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        Services.AddBrowserStorage();
        _plan = BrowserStoragePlan.On(JSInterop);
    }

    /// <summary>The three operations, for the tests that hold for each.</summary>
    public enum Operation { Read, Write, Remove }

    /// <summary>How a declared call and the call made differ.</summary>
    public enum Mismatch { Area, Key, Value, Operation }

    /// <summary>The three read answers, as a theory can name them.</summary>
    public enum ReadKind { Stored, Absent, Refused }

    private BrowserStorage Storage => Services.GetRequiredService<BrowserStorage>();

    public static TheoryData<BrowserStorageArea> Areas => new(Enum.GetValues<BrowserStorageArea>());

    public static TheoryData<BrowserStorageArea, string> AreasAndStoredValues
    {
        get
        {
            var data = new TheoryData<BrowserStorageArea, string>();
            foreach (var area in Enum.GetValues<BrowserStorageArea>())
            {
                data.Add(area, Value);
                data.Add(area, "");
            }

            return data;
        }
    }

    public static TheoryData<Operation, BrowserStorageArea> EveryOperationInEveryArea
    {
        get
        {
            var data = new TheoryData<Operation, BrowserStorageArea>();
            foreach (var operation in Enum.GetValues<Operation>())
            {
                foreach (var area in Enum.GetValues<BrowserStorageArea>())
                {
                    data.Add(operation, area);
                }
            }

            return data;
        }
    }

    public static TheoryData<Operation, BrowserStorageArea, JSRuntimeMode> EveryOperationInEveryAreaAndMode
    {
        get
        {
            var data = new TheoryData<Operation, BrowserStorageArea, JSRuntimeMode>();
            foreach (var operation in Enum.GetValues<Operation>())
            {
                foreach (var area in Enum.GetValues<BrowserStorageArea>())
                {
                    foreach (var mode in Enum.GetValues<JSRuntimeMode>())
                    {
                        data.Add(operation, area, mode);
                    }
                }
            }

            return data;
        }
    }

    public static TheoryData<Mismatch> Mismatches => new(Enum.GetValues<Mismatch>());

    public static TheoryData<ReadKind> ReadKinds => new(Enum.GetValues<ReadKind>());

    public static TheoryData<bool> Refusals => new(false, true);

    // ---- Answers, read by the real accessor ------------------------------------

    // A stored answer is the accessor's stored result, its value exactly —
    // the empty string included, which must never become absent.
    [Theory]
    [MemberData(nameof(AreasAndStoredValues))]
    public async Task AStoredAnswer_IsReadAsStored_WithItsValue(BrowserStorageArea area, string value)
    {
        _plan.ExpectRead(area, Key, BrowserStorageReadAnswer.Stored(value));

        Assert.Equal($"stored '{value}'", Outcome(await AnsweredAtTheCall(Storage.ReadAsync(area, Key))));
        _plan.Verify();
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public async Task AnAbsentAnswer_IsReadAsAbsent(BrowserStorageArea area)
    {
        _plan.ExpectRead(area, Key, BrowserStorageReadAnswer.Absent);

        Assert.Equal("absent", Outcome(await AnsweredAtTheCall(Storage.ReadAsync(area, Key))));
        _plan.Verify();
    }

    // A refusal reaches the accessor as the browser's does — a JSException —
    // so it is a refused result, never an exception from the call.
    [Theory]
    [MemberData(nameof(EveryOperationInEveryArea))]
    public async Task ARefusedAnswer_IsARefusal_ForEveryOperation_InEveryArea(Operation operation, BrowserStorageArea area)
    {
        Expect(operation, area, refused: true);

        Assert.Equal("refused", await RunAsync(operation, area));
        _plan.Verify();
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public async Task ASucceededAnswer_IsASuccess_ForAWriteAndARemoval(BrowserStorageArea area)
    {
        _plan.ExpectWrite(area, Key, Value, BrowserStorageWriteAnswer.Succeeded);
        _plan.ExpectWrite(area, Key, "", BrowserStorageWriteAnswer.Succeeded);
        _plan.ExpectRemove(area, Key, BrowserStorageWriteAnswer.Succeeded);

        Assert.Equal("succeeded", Outcome(await AnsweredAtTheCall(Storage.WriteAsync(area, Key, Value))));
        Assert.Equal("succeeded", Outcome(await AnsweredAtTheCall(Storage.WriteAsync(area, Key, ""))));
        Assert.Equal("succeeded", Outcome(await AnsweredAtTheCall(Storage.RemoveAsync(area, Key))));
        _plan.Verify();
    }

    // Each refusal is raised for its own call, as a browser raises each.
    [Fact]
    public async Task EachRefusal_IsItsOwnException()
    {
        _plan.ExpectRead(BrowserStorageArea.Local, Key, BrowserStorageReadAnswer.Refused, times: 2);

        var first = await AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key));
        var second = await AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key));

        Assert.NotSame(first.Refusal, second.Refusal);
    }

    // ---- Successive answers --------------------------------------------------

    // Answers declared for one call are given in the order declared: a
    // refusal, then a success, and the other way round.
    [Theory]
    [MemberData(nameof(EveryOperationInEveryArea))]
    public async Task ARefusalThenASuccess_AreAnsweredInTheOrderDeclared(Operation operation, BrowserStorageArea area)
    {
        Expect(operation, area, refused: true);
        Expect(operation, area, refused: false);

        Assert.Equal("refused", await RunAsync(operation, area));
        Assert.Equal(Accepted(operation), await RunAsync(operation, area));
        _plan.Verify();
    }

    [Theory]
    [MemberData(nameof(EveryOperationInEveryArea))]
    public async Task ASuccessThenARefusal_AreAnsweredInTheOrderDeclared(Operation operation, BrowserStorageArea area)
    {
        Expect(operation, area, refused: false);
        Expect(operation, area, refused: true);

        Assert.Equal(Accepted(operation), await RunAsync(operation, area));
        Assert.Equal("refused", await RunAsync(operation, area));
        _plan.Verify();
    }

    // Writes are matched by their value, so writes of different values are
    // independent and each gets its own answer, in whatever order they come.
    [Fact]
    public async Task WritesOfDifferentValues_AreAnsweredByValue_InAnyOrder()
    {
        _plan.ExpectWrite(BrowserStorageArea.Local, Key, Value, BrowserStorageWriteAnswer.Succeeded);
        _plan.ExpectWrite(BrowserStorageArea.Local, Key, OtherValue, BrowserStorageWriteAnswer.Refused);

        Assert.Equal("refused", Outcome(await AnsweredAtTheCall(Storage.WriteAsync(BrowserStorageArea.Local, Key, OtherValue))));
        Assert.Equal("succeeded", Outcome(await AnsweredAtTheCall(Storage.WriteAsync(BrowserStorageArea.Local, Key, Value))));
        _plan.Verify();
    }

    // ---- Calls the plan does not expect --------------------------------------

    // Detected at the call: the accessor does not catch the plan's failure,
    // so the operation throws it — in either JSInterop mode, since the plan
    // answers storage calls whatever the mode. And recorded, so Verify
    // reports it too.
    [Theory]
    [MemberData(nameof(EveryOperationInEveryAreaAndMode))]
    public async Task AnUnplannedCall_FailsAtTheCall_AndAtVerify(
        Operation operation, BrowserStorageArea area, JSRuntimeMode mode)
    {
        JSInterop.Mode = mode;

        await Assert.ThrowsAsync<BrowserStorageAssertionException>(() => RunAsync(operation, area));

        var failure = Assert.Throws<BrowserStorageAssertionException>(_plan.Verify);
        Assert.Contains("was not expected", failure.Message);
    }

    // Code under test that swallows every exception hides the call's failure;
    // Verify still finds the call.
    [Fact]
    public async Task AnUnplannedCall_TheCodeUnderTestCaught_IsStillReportedByVerify()
    {
        try
        {
            await AnsweredAtTheCall(Storage.WriteAsync(BrowserStorageArea.Session, Key, Value));
        }
        catch (BrowserStorageAssertionException)
        {
            // What a catch-all in the code under test would do.
        }

        var failure = Assert.Throws<BrowserStorageAssertionException>(_plan.Verify);
        Assert.Contains($"a write of '{Value}' to Session '{Key}'", failure.Message);
    }

    // ---- Verify: the control, and each way it fails ----------------------------

    // The control: every planned call made, in an order other than declared,
    // passes. Independent operations are never forced into a sequence.
    [Fact]
    public async Task Verify_Passes_WhenEveryPlannedCallIsMade_InAnyOrder()
    {
        _plan.ExpectRead(BrowserStorageArea.Local, Key, BrowserStorageReadAnswer.Stored(Value));
        _plan.ExpectWrite(BrowserStorageArea.Session, OtherKey, Value, BrowserStorageWriteAnswer.Succeeded);
        _plan.ExpectRemove(BrowserStorageArea.Local, OtherKey, BrowserStorageWriteAnswer.Succeeded);

        await AnsweredAtTheCall(Storage.RemoveAsync(BrowserStorageArea.Local, OtherKey));
        await AnsweredAtTheCall(Storage.WriteAsync(BrowserStorageArea.Session, OtherKey, Value));
        await AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key));

        _plan.Verify();
    }

    [Theory]
    [MemberData(nameof(EveryOperationInEveryArea))]
    public void Verify_Fails_ForAnOmittedCall(Operation operation, BrowserStorageArea area)
    {
        Expect(operation, area, refused: false);

        var failure = Assert.Throws<BrowserStorageAssertionException>(_plan.Verify);
        Assert.Contains("Expected and not made", failure.Message);
    }

    // One call more than expected — the same call again — fails at the call
    // and at Verify.
    [Theory]
    [MemberData(nameof(EveryOperationInEveryArea))]
    public async Task AnExtraMatchingCall_FailsAtTheCall_AndAtVerify(Operation operation, BrowserStorageArea area)
    {
        Expect(operation, area, refused: false);
        await RunAsync(operation, area);

        await Assert.ThrowsAsync<BrowserStorageAssertionException>(() => RunAsync(operation, area));

        var failure = Assert.Throws<BrowserStorageAssertionException>(_plan.Verify);
        Assert.Contains("one more than the 1 the plan expects", failure.Message);
    }

    // A call that differs from the planned one in any one respect is not that
    // call: it fails at the call, and Verify reports it and the planned call
    // that was not made.
    [Theory]
    [MemberData(nameof(Mismatches))]
    public async Task ACallWithWrongArguments_FailsAtTheCall_AndAtVerify(Mismatch mismatch)
    {
        _plan.ExpectWrite(BrowserStorageArea.Local, Key, Value, BrowserStorageWriteAnswer.Succeeded);

        Func<Task> made = mismatch switch
        {
            Mismatch.Area => () => AnsweredAtTheCall(Storage.WriteAsync(BrowserStorageArea.Session, Key, Value)),
            Mismatch.Key => () => AnsweredAtTheCall(Storage.WriteAsync(BrowserStorageArea.Local, OtherKey, Value)),
            Mismatch.Value => () => AnsweredAtTheCall(Storage.WriteAsync(BrowserStorageArea.Local, Key, OtherValue)),
            Mismatch.Operation => () => AnsweredAtTheCall(Storage.RemoveAsync(BrowserStorageArea.Local, Key)),
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, null),
        };

        await Assert.ThrowsAsync<BrowserStorageAssertionException>(made);

        var failure = Assert.Throws<BrowserStorageAssertionException>(_plan.Verify);
        Assert.Contains("was not expected", failure.Message);
        Assert.Contains($"Expected and not made: a write of '{Value}' to Local '{Key}'", failure.Message);
    }

    // A read's operation is checked too: a read is not a removal of its key.
    [Fact]
    public async Task AReadOfAKeyPlannedForRemoval_FailsAtTheCall()
    {
        _plan.ExpectRemove(BrowserStorageArea.Local, Key, BrowserStorageWriteAnswer.Succeeded);

        await Assert.ThrowsAsync<BrowserStorageAssertionException>(() => AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key)));
    }

    // A stated number of calls is exactly that number.
    [Fact]
    public async Task Times_IsExactlyThatManyCalls()
    {
        _plan.ExpectRead(BrowserStorageArea.Local, Key, BrowserStorageReadAnswer.Absent, times: 3);

        await AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key));
        await AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key));

        var tooFew = Assert.Throws<BrowserStorageAssertionException>(_plan.Verify);
        Assert.Contains("Made 2 of the 3 times expected", tooFew.Message);

        await AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key));
        _plan.Verify();

        await Assert.ThrowsAsync<BrowserStorageAssertionException>(() => AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key)));
    }

    // ---- Order, only where the test requires one -----------------------------

    [Fact]
    public async Task ARequiredOrder_Kept_Passes()
    {
        var first = _plan.ExpectRead(BrowserStorageArea.Local, Key, BrowserStorageReadAnswer.Absent);
        var second = _plan.ExpectWrite(BrowserStorageArea.Local, OtherKey, Value, BrowserStorageWriteAnswer.Succeeded);
        _plan.RequireOrder(first, second);

        await AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key));
        await AnsweredAtTheCall(Storage.WriteAsync(BrowserStorageArea.Local, OtherKey, Value));

        _plan.Verify();
    }

    [Fact]
    public async Task ARequiredOrder_Broken_FailsVerify()
    {
        var first = _plan.ExpectRead(BrowserStorageArea.Local, Key, BrowserStorageReadAnswer.Absent);
        var second = _plan.ExpectWrite(BrowserStorageArea.Local, OtherKey, Value, BrowserStorageWriteAnswer.Succeeded);
        _plan.RequireOrder(first, second);

        await AnsweredAtTheCall(Storage.WriteAsync(BrowserStorageArea.Local, OtherKey, Value));
        await AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key));

        var failure = Assert.Throws<BrowserStorageAssertionException>(_plan.Verify);
        Assert.Contains("Out of the required order", failure.Message);
    }

    [Fact]
    public void RequireOrder_RefusesWhatIsNoOrder()
    {
        var first = _plan.ExpectRead(BrowserStorageArea.Local, Key, BrowserStorageReadAnswer.Absent);
        var second = _plan.ExpectRead(BrowserStorageArea.Local, OtherKey, BrowserStorageReadAnswer.Absent);
        using var otherContext = new BunitContext();
        var foreign = BrowserStoragePlan.On(otherContext.JSInterop)
            .ExpectRead(BrowserStorageArea.Local, Key, BrowserStorageReadAnswer.Absent);

        Assert.Throws<ArgumentNullException>(() => _plan.RequireOrder(null!));
        Assert.Throws<ArgumentException>(() => _plan.RequireOrder(first));
        Assert.Throws<ArgumentException>(() => _plan.RequireOrder(first, null!));
        Assert.Throws<ArgumentException>(() => _plan.RequireOrder(first, first));
        Assert.Throws<ArgumentException>(() => _plan.RequireOrder(first, foreign));
        _plan.RequireOrder(first, second);
    }

    // ---- Held calls ------------------------------------------------------------

    // A held read is reached when the accessor makes it, stays pending — the
    // operation has not completed — and completes with the answer the test
    // releases. No sleep: the pending task is the evidence.
    [Theory]
    [MemberData(nameof(ReadKinds))]
    public async Task AHeldRead_StaysPending_UntilTheTestReleasesIt(ReadKind kind)
    {
        var held = _plan.ExpectHeldRead(BrowserStorageArea.Local, Key);
        Assert.False(held.IsReached);

        var read = Storage.ReadAsync(BrowserStorageArea.Local, Key);

        Assert.True(held.IsReached);
        Assert.False(read.IsCompleted);

        held.Release(ReadAnswer(kind));

        Assert.Equal(ExpectedOutcome(kind), Outcome(await CompletedAfterRelease(read)));
        _plan.Verify();
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task AHeldWrite_StaysPending_UntilTheTestReleasesIt(bool refused)
    {
        var held = _plan.ExpectHeldWrite(BrowserStorageArea.Session, Key, Value);
        Assert.False(held.IsReached);

        var write = Storage.WriteAsync(BrowserStorageArea.Session, Key, Value);

        Assert.True(held.IsReached);
        Assert.False(write.IsCompleted);

        held.Release(refused ? BrowserStorageWriteAnswer.Refused : BrowserStorageWriteAnswer.Succeeded);

        Assert.Equal(refused ? "refused" : "succeeded", Outcome(await CompletedAfterRelease(write)));
        _plan.Verify();
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task AHeldRemoval_StaysPending_UntilTheTestReleasesIt(bool refused)
    {
        var held = _plan.ExpectHeldRemove(BrowserStorageArea.Local, Key);

        var removal = Storage.RemoveAsync(BrowserStorageArea.Local, Key);

        Assert.True(held.IsReached);
        Assert.False(removal.IsCompleted);

        held.Release(refused ? BrowserStorageWriteAnswer.Refused : BrowserStorageWriteAnswer.Succeeded);

        Assert.Equal(refused ? "refused" : "succeeded", Outcome(await CompletedAfterRelease(removal)));
        _plan.Verify();
    }

    // Two overlapping reads of one key are two calls: each held read is its
    // own, matched in the order declared, and the newer may complete first.
    [Fact]
    public async Task OverlappingHeldReads_OfOneKey_AreToldApart_AndTheOlderMayCompleteLast()
    {
        var older = _plan.ExpectHeldRead(BrowserStorageArea.Local, Key);
        var newer = _plan.ExpectHeldRead(BrowserStorageArea.Local, Key);

        var olderRead = Storage.ReadAsync(BrowserStorageArea.Local, Key);
        Assert.True(older.IsReached);
        Assert.False(newer.IsReached);

        var newerRead = Storage.ReadAsync(BrowserStorageArea.Local, Key);
        Assert.True(newer.IsReached);

        newer.Release(BrowserStorageReadAnswer.Stored("newer"));
        Assert.Equal("stored 'newer'", Outcome(await CompletedAfterRelease(newerRead)));
        Assert.False(olderRead.IsCompleted);

        older.Release(BrowserStorageReadAnswer.Stored("older"));
        Assert.Equal("stored 'older'", Outcome(await CompletedAfterRelease(olderRead)));
        _plan.Verify();
    }

    // The same for saves: an older write still pending when a newer one
    // completes, then completing itself.
    [Fact]
    public async Task OverlappingHeldWrites_AreToldApart_AndTheOlderMayCompleteLast()
    {
        var older = _plan.ExpectHeldWrite(BrowserStorageArea.Local, Key, Value);
        var newer = _plan.ExpectHeldWrite(BrowserStorageArea.Local, Key, OtherValue);

        var olderWrite = Storage.WriteAsync(BrowserStorageArea.Local, Key, Value);
        var newerWrite = Storage.WriteAsync(BrowserStorageArea.Local, Key, OtherValue);
        Assert.True(older.IsReached);
        Assert.True(newer.IsReached);

        newer.Release(BrowserStorageWriteAnswer.Succeeded);
        Assert.Equal("succeeded", Outcome(await CompletedAfterRelease(newerWrite)));
        Assert.False(olderWrite.IsCompleted);

        older.Release(BrowserStorageWriteAnswer.Refused);
        Assert.Equal("refused", Outcome(await CompletedAfterRelease(olderWrite)));
        _plan.Verify();
    }

    // Held and immediate answers for one call are one sequence, in the order
    // declared.
    [Fact]
    public async Task AHeldRead_DeclaredAfterARefusal_IsTheSecondCall()
    {
        _plan.ExpectRead(BrowserStorageArea.Local, Key, BrowserStorageReadAnswer.Refused);
        var held = _plan.ExpectHeldRead(BrowserStorageArea.Local, Key);

        Assert.Equal("refused", Outcome(await AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key))));
        Assert.False(held.IsReached);

        var second = Storage.ReadAsync(BrowserStorageArea.Local, Key);
        Assert.True(held.IsReached);

        held.Release(BrowserStorageReadAnswer.Stored(Value));
        Assert.Equal($"stored '{Value}'", Outcome(await CompletedAfterRelease(second)));
        _plan.Verify();
    }

    [Fact]
    public async Task ReleasingAHeldCall_BeforeItIsReached_Fails_AndTwice_Throws()
    {
        var held = _plan.ExpectHeldRead(BrowserStorageArea.Local, Key);

        var early = Assert.Throws<BrowserStorageAssertionException>(
            () => held.Release(BrowserStorageReadAnswer.Absent));
        Assert.Contains("has not been reached", early.Message);

        var read = Storage.ReadAsync(BrowserStorageArea.Local, Key);
        held.Release(BrowserStorageReadAnswer.Absent);
        var twice = Assert.Throws<InvalidOperationException>(() => held.Release(BrowserStorageReadAnswer.Absent));
        Assert.Contains("has already been released", twice.Message);

        Assert.Equal("absent", Outcome(await CompletedAfterRelease(read)));
    }

    // Verify counts a held call as made once it is reached, released or not;
    // one never reached was not made. The pending operation is kept, and
    // released and awaited in cleanup, so the test leaves nothing behind.
    [Fact]
    public async Task AHeldCall_ReachedAndStillPending_PassesVerify()
    {
        var held = _plan.ExpectHeldWrite(BrowserStorageArea.Local, Key, Value);
        var write = Storage.WriteAsync(BrowserStorageArea.Local, Key, Value);

        try
        {
            Assert.False(write.IsCompleted);
            _plan.Verify();
        }
        finally
        {
            held.Release(BrowserStorageWriteAnswer.Succeeded);
            await CompletedAfterRelease(write);
        }
    }

    [Fact]
    public void AHeldCall_NeverReached_FailsVerify()
    {
        _plan.ExpectHeldRemove(BrowserStorageArea.Session, Key);

        var failure = Assert.Throws<BrowserStorageAssertionException>(_plan.Verify);
        Assert.Contains($"Expected and not made: the held removal of Session '{Key}'", failure.Message);
    }

    // ---- What the plan leaves alone --------------------------------------------

    // The test's other plans answer their own calls — a specific one and a
    // catch-all alike — and bUnit's record of every invocation, storage and
    // other, stays whole.
    [Fact]
    public async Task OtherPlans_AndTheInvocationRecord_AreLeftIntact()
    {
        JSInterop.Setup<string?>("app.title").SetResult("Title");
        JSInterop.Setup<string?>(_ => true, isCatchAllHandler: true).SetResult("from the catch-all");
        _plan.ExpectRead(BrowserStorageArea.Local, Key, BrowserStorageReadAnswer.Stored(Value));

        Assert.Equal($"stored '{Value}'", Outcome(await AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key))));
        Assert.Equal("Title", await JSInterop.JSRuntime.InvokeAsync<string?>("app.title"));
        Assert.Equal("from the catch-all", await JSInterop.JSRuntime.InvokeAsync<string?>("app.other"));

        Assert.Equal(3, JSInterop.Invocations.Count);
        Assert.Single(JSInterop.Invocations["localStorage.getItem"]);
        Assert.Single(JSInterop.Invocations["app.title"]);
        _plan.Verify();
    }

    // An unplanned call that is not a storage call stays bUnit's: strict mode
    // still refuses it, and it is not the plan's to report.
    [Fact]
    public async Task AnUnplannedCall_ThatIsNotStorage_IsLeftToBUnit()
    {
        await Assert.ThrowsAsync<JSRuntimeUnhandledInvocationException>(
            () => JSInterop.JSRuntime.InvokeAsync<string?>("app.unplanned").AsTask());

        _plan.Verify();
    }

    // A storage call answered by a handler the test registered itself — which
    // spells the identifier, the thing the plan exists to prevent — is still
    // a call the plan must account for, and Verify says it did not.
    [Fact]
    public async Task AStorageCall_AnsweredByAnotherHandler_FailsVerify()
    {
        JSInterop.Setup<string?>("localStorage.getItem", Key).SetResult("bypassed");

        Assert.Equal("stored 'bypassed'", Outcome(await AnsweredAtTheCall(Storage.ReadAsync(BrowserStorageArea.Local, Key))));

        var failure = Assert.Throws<BrowserStorageAssertionException>(_plan.Verify);
        Assert.Contains("A storage call the plan did not answer", failure.Message);
    }

    // The plan reads the calls it answers as the accessor's three operations;
    // a call to a storage identifier in any other shape is none of them, and
    // is unexpected however the plan is set.
    [Fact]
    public async Task ACallTheAccessorNeverMakes_IsUnexpected()
    {
        _plan.ExpectRead(BrowserStorageArea.Local, Key, BrowserStorageReadAnswer.Absent);
        _plan.ExpectWrite(BrowserStorageArea.Local, Key, Value, BrowserStorageWriteAnswer.Succeeded);

        await Assert.ThrowsAsync<BrowserStorageAssertionException>(
            () => JSInterop.JSRuntime.InvokeVoidAsync("localStorage.getItem", Key).AsTask());
        await Assert.ThrowsAsync<BrowserStorageAssertionException>(
            () => JSInterop.JSRuntime.InvokeAsync<string?>("localStorage.setItem", Key, Value).AsTask());
        await Assert.ThrowsAsync<BrowserStorageAssertionException>(
            () => JSInterop.JSRuntime.InvokeVoidAsync("localStorage.setItem", Key).AsTask());

        var failure = Assert.Throws<BrowserStorageAssertionException>(_plan.Verify);
        Assert.Contains("a call BrowserStorage never makes", failure.Message);
    }

    // ---- Registration and usage errors -------------------------------------------

    [Fact]
    public void ASecondPlan_OnOneJSInterop_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => BrowserStoragePlan.On(JSInterop));
    }

    [Fact]
    public void On_ANullJSInterop_Throws()
    {
        var thrown = Assert.Throws<ArgumentNullException>(() => BrowserStoragePlan.On(null!));

        Assert.Equal("jsInterop", thrown.ParamName);
    }

    [Fact]
    public void Expecting_AnUndeclaredArea_Throws_AndExpectsNothing()
    {
        const BrowserStorageArea undeclared = (BrowserStorageArea)(-1);
        var answer = BrowserStorageReadAnswer.Absent;
        var accepted = BrowserStorageWriteAnswer.Succeeded;

        Assert.Equal("area", Assert.Throws<ArgumentOutOfRangeException>(() => _plan.ExpectRead(undeclared, Key, answer)).ParamName);
        Assert.Equal("area", Assert.Throws<ArgumentOutOfRangeException>(() => _plan.ExpectWrite(undeclared, Key, Value, accepted)).ParamName);
        Assert.Equal("area", Assert.Throws<ArgumentOutOfRangeException>(() => _plan.ExpectRemove(undeclared, Key, accepted)).ParamName);
        Assert.Equal("area", Assert.Throws<ArgumentOutOfRangeException>(() => _plan.ExpectHeldRead(undeclared, Key)).ParamName);
        Assert.Equal("area", Assert.Throws<ArgumentOutOfRangeException>(() => _plan.ExpectHeldWrite(undeclared, Key, Value)).ParamName);
        Assert.Equal("area", Assert.Throws<ArgumentOutOfRangeException>(() => _plan.ExpectHeldRemove(undeclared, Key)).ParamName);

        _plan.Verify();
    }

    [Fact]
    public void Expecting_ANullArgument_OrNoCall_Throws_AndExpectsNothing()
    {
        const BrowserStorageArea area = BrowserStorageArea.Local;
        var answer = BrowserStorageReadAnswer.Absent;
        var accepted = BrowserStorageWriteAnswer.Succeeded;

        Assert.Equal("key", Assert.Throws<ArgumentNullException>(() => _plan.ExpectRead(area, null!, answer)).ParamName);
        Assert.Equal("answer", Assert.Throws<ArgumentNullException>(() => _plan.ExpectRead(area, Key, null!)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => _plan.ExpectWrite(area, Key, null!, accepted)).ParamName);
        Assert.Equal("answer", Assert.Throws<ArgumentNullException>(() => _plan.ExpectRemove(area, Key, null!)).ParamName);
        Assert.Equal("key", Assert.Throws<ArgumentNullException>(() => _plan.ExpectHeldRead(area, null!)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => _plan.ExpectHeldWrite(area, Key, null!)).ParamName);
        Assert.Equal("key", Assert.Throws<ArgumentNullException>(() => _plan.ExpectHeldRemove(area, null!)).ParamName);
        Assert.Equal("times", Assert.Throws<ArgumentOutOfRangeException>(() => _plan.ExpectRead(area, Key, answer, times: 0)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => BrowserStorageReadAnswer.Stored(null!)).ParamName);

        _plan.Verify();
    }

    // ---- Helpers -------------------------------------------------------------

    private void Expect(Operation operation, BrowserStorageArea area, bool refused)
    {
        var written = refused ? BrowserStorageWriteAnswer.Refused : BrowserStorageWriteAnswer.Succeeded;
        switch (operation)
        {
            case Operation.Read:
                _plan.ExpectRead(area, Key, refused ? BrowserStorageReadAnswer.Refused : BrowserStorageReadAnswer.Stored(Value));
                break;
            case Operation.Write:
                _plan.ExpectWrite(area, Key, Value, written);
                break;
            case Operation.Remove:
                _plan.ExpectRemove(area, Key, written);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    // An answer declared with its call completes the accessor's operation at
    // the call. Asserted before awaiting, so a planner that wrongly held a
    // call fails here rather than hanging the test.
    private static async Task<T> AnsweredAtTheCall<T>(Task<T> operation)
    {
        Assert.True(operation.IsCompleted, "The operation is still pending: the plan held a call it should have answered.");
        return await operation;
    }

    // A held operation resumes asynchronously after its release, so it is
    // awaited — within bUnit's normal wait, BunitContext.DefaultWaitTimeout,
    // so that a release that never completed the call fails the test with a
    // TimeoutException instead of hanging it. The deadline only guards
    // against a hang: the reached, pending and completed states asserted
    // around it are the evidence.
    private static Task<T> CompletedAfterRelease<T>(Task<T> operation) =>
        operation.WaitAsync(BunitContext.DefaultWaitTimeout);

    private static string Accepted(Operation operation) =>
        operation == Operation.Read ? $"stored '{Value}'" : "succeeded";

    // Runs the operation through the real accessor and names its outcome.
    private async Task<string> RunAsync(Operation operation, BrowserStorageArea area) => operation switch
    {
        Operation.Read => Outcome(await AnsweredAtTheCall(Storage.ReadAsync(area, Key))),
        Operation.Write => Outcome(await AnsweredAtTheCall(Storage.WriteAsync(area, Key, Value))),
        Operation.Remove => Outcome(await AnsweredAtTheCall(Storage.RemoveAsync(area, Key))),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
    };

    private static BrowserStorageReadAnswer ReadAnswer(ReadKind kind) => kind switch
    {
        ReadKind.Stored => BrowserStorageReadAnswer.Stored(Value),
        ReadKind.Absent => BrowserStorageReadAnswer.Absent,
        ReadKind.Refused => BrowserStorageReadAnswer.Refused,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static string ExpectedOutcome(ReadKind kind) => kind switch
    {
        ReadKind.Stored => $"stored '{Value}'",
        ReadKind.Absent => "absent",
        _ => "refused",
    };

    // The accessor's result, named — and checked to be exactly one outcome,
    // with a refusal that is a browser's JSException.
    private static string Outcome(BrowserStorageReadResult read)
    {
        Assert.Equal(1, new[] { read.IsStored, read.IsAbsent, read.IsRefused }.Count(flag => flag));

        if (read.IsRefused)
        {
            Assert.IsType<JSException>(read.Refusal);
            return "refused";
        }

        return read.IsStored ? $"stored '{read.Value}'" : "absent";
    }

    private static string Outcome(BrowserStorageWriteResult written)
    {
        Assert.NotEqual(written.Succeeded, written.IsRefused);

        if (written.IsRefused)
        {
            Assert.IsType<JSException>(written.Refusal);
            return "refused";
        }

        return "succeeded";
    }
}
