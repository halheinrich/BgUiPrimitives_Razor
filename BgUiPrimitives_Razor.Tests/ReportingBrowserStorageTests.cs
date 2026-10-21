using BgDataTypes_Lib.TestSupport;
using BgUiPrimitives_Razor.TestSupport;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace BgUiPrimitives_Razor.Tests;

// Pins for ReportingBrowserStorage: each operation makes the accessor's one
// call; a refusal is reported to the condition before the result returns and
// logged once in the caller's words; the result is the accessor's own; a
// success reports and logs nothing; a throwing logger costs neither the
// occurrence nor the result; anything the accessor propagates propagates from
// here, reported to nothing.
//
// The real BrowserStorage runs, answered through BrowserStoragePlan in the
// accessor's vocabulary, as a consumer's test plans it. The one case the plan
// cannot stage — a failure that is not the browser's refusal — runs the real
// accessor over a runtime that refuses every interop call, as static
// rendering's does.
public class ReportingBrowserStorageTests : BunitContext
{
    private const BrowserStorageArea Area = BrowserStorageArea.Local;
    private const string Key = "bg.test.key";
    private const string Value = "stored value";
    private const string Consequence = "the mix panel starts blank for this visit";

    private readonly BrowserStoragePlan _plan;
    private readonly BrowserStorageCondition _condition = new();
    private readonly RecordingLogger<ReportingBrowserStorage> _logger = new();

    public ReportingBrowserStorageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        Services.AddBrowserStorage();
        _plan = BrowserStoragePlan.On(JSInterop);
    }

    /// <summary>The three operations, for the tests that hold for each.</summary>
    public enum Operation { Read, Write, Remove }

    public static TheoryData<Operation> Operations => new(Enum.GetValues<Operation>());

    public static TheoryData<Operation, string?> OperationsAndBlankConsequences
    {
        get
        {
            var data = new TheoryData<Operation, string?>();
            foreach (var operation in Enum.GetValues<Operation>())
            {
                data.Add(operation, null);
                data.Add(operation, "");
                data.Add(operation, " \t");
            }

            return data;
        }
    }

    private BrowserStorage Storage => Services.GetRequiredService<BrowserStorage>();

    private ReportingBrowserStorage Access(ILogger<ReportingBrowserStorage>? logger = null) =>
        new(Storage, _condition, logger ?? _logger);

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ARefusal_IsReportedBeforeTheResultReturns_AndLoggedOnce(Operation operation)
    {
        ExpectRefused(operation);
        var events = new List<string>();
        using var _ = _condition.Attach(() => { if (_condition.HasOccurred) events.Add("reported"); });

        var outcome = await Run(Access(), operation);
        events.Add("returned");

        Assert.Equal(["reported", "returned"], events);
        Assert.True(_condition.HasOccurred);
        Assert.False(_condition.IsNoticeDismissed);

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(
            $"The browser refused a storage {OperationWord(operation)} of the key {Key}; {Consequence}.",
            entry.Message);
        Assert.Same(outcome.Refusal, entry.Exception);
        _plan.Verify();
    }

    // The result is the accessor's own: refused, carrying the very exception
    // the browser raised — the one the planner hands the accessor, and the
    // one logged.
    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ARefusal_ReturnsTheAccessorsOwnResult(Operation operation)
    {
        ExpectRefused(operation);

        var outcome = await Run(Access(), operation);

        Assert.True(outcome.IsRefused);
        Assert.NotNull(outcome.Refusal);
        Assert.Same(outcome.Refusal, Assert.Single(_logger.Entries).Exception);
        switch (outcome.Result)
        {
            case BrowserStorageReadResult read:
                Assert.False(read.IsStored);
                Assert.False(read.IsAbsent);
                Assert.Null(read.Value);
                break;
            case BrowserStorageWriteResult written:
                Assert.False(written.Succeeded);
                break;
            default:
                Assert.Fail($"Unexpected result {outcome.Result}.");
                break;
        }
    }

    [Fact]
    public async Task AStoredRead_ReportsAndLogsNothing()
    {
        _plan.ExpectRead(Area, Key, BrowserStorageReadAnswer.Stored(Value));

        var read = await Access().ReadAsync(Area, Key, Consequence);

        Assert.True(read.IsStored);
        Assert.Equal(Value, read.Value);
        AssertNothingReported();
        _plan.Verify();
    }

    [Fact]
    public async Task AnAbsentRead_ReportsAndLogsNothing()
    {
        _plan.ExpectRead(Area, Key, BrowserStorageReadAnswer.Absent);

        var read = await Access().ReadAsync(Area, Key, Consequence);

        Assert.True(read.IsAbsent);
        AssertNothingReported();
        _plan.Verify();
    }

    [Fact]
    public async Task ASucceededWrite_ReportsAndLogsNothing()
    {
        _plan.ExpectWrite(Area, Key, Value, BrowserStorageWriteAnswer.Succeeded);

        var written = await Access().WriteAsync(Area, Key, Value, Consequence);

        Assert.True(written.Succeeded);
        AssertNothingReported();
        _plan.Verify();
    }

    [Fact]
    public async Task ASucceededRemoval_ReportsAndLogsNothing()
    {
        _plan.ExpectRemove(Area, Key, BrowserStorageWriteAnswer.Succeeded);

        var removed = await Access().RemoveAsync(Area, Key, Consequence);

        Assert.True(removed.Succeeded);
        AssertNothingReported();
        _plan.Verify();
    }

    // The occurrence is recorded before the warning is attempted, and the
    // logger's failure is contained around the logging call alone: the
    // observers are told and the refused result returns normally.
    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ALoggerThatThrows_LeavesTheOccurrenceRecorded_AndTheResultNormal(Operation operation)
    {
        ExpectRefused(operation);
        var logger = new ThrowingLogger<ReportingBrowserStorage>();
        var notified = 0;
        using var _ = _condition.Attach(() => notified++);
        notified = 0;

        var outcome = await Run(Access(logger), operation);

        Assert.True(outcome.IsRefused);
        Assert.True(_condition.HasOccurred);
        Assert.Equal(1, notified);
        Assert.Equal(1, logger.Attempts);
        _plan.Verify();
    }

    [Fact]
    public async Task ASecondRefusal_IsTheSameOccurrence_AndIsLoggedAgain()
    {
        _plan.ExpectRead(Area, Key, BrowserStorageReadAnswer.Refused);
        _plan.ExpectWrite(Area, Key, Value, BrowserStorageWriteAnswer.Refused);
        var notified = 0;
        using var _ = _condition.Attach(() => notified++);
        notified = 0;
        var access = Access();

        await access.ReadAsync(Area, Key, Consequence);
        await access.WriteAsync(Area, Key, Value, "the mix is kept for this visit only");

        Assert.Equal(1, notified);
        Assert.Collection(_logger.Entries,
            e => Assert.Equal($"The browser refused a storage read of the key {Key}; {Consequence}.", e.Message),
            e => Assert.Equal(
                $"The browser refused a storage write of the key {Key}; the mix is kept for this visit only.",
                e.Message));
        _plan.Verify();
    }

    // A failure that is not the browser's refusal — here static rendering's
    // runtime, which refuses every interop call — propagates from here
    // exactly as the accessor propagates it, and is reported to nothing.
    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ANonRefusalFailure_Propagates_AndIsReportedToNothing(Operation operation)
    {
        var runtime = new NoInteropRuntime();
        var access = new ReportingBrowserStorage(new BrowserStorage(runtime), _condition, _logger);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Run(access, operation));

        Assert.Same(runtime.Failure, thrown);
        AssertNothingReported();
    }

    [Theory]
    [MemberData(nameof(OperationsAndBlankConsequences))]
    public void ABlankConsequence_ThrowsAtTheCall_BeforeAnyCall(Operation operation, string? consequence)
    {
        var access = Access();

        var thrown = Assert.ThrowsAny<ArgumentException>(() => { _ = Run(access, operation, consequence!); });

        Assert.Equal("consequence", thrown.ParamName);
        AssertNothingReported();
        _plan.Verify(); // nothing planned, so any call made would fail here
    }

    // The accessor's own usage errors throw synchronously from here, before
    // any call, reported to nothing.
    [Theory]
    [MemberData(nameof(Operations))]
    public void TheAccessorsArgumentError_ThrowsAtTheCall_Unreported(Operation operation)
    {
        var access = Access();

        var thrown = Assert.Throws<ArgumentNullException>(() => { _ = Run(access, operation, key: null!); });

        Assert.Equal("key", thrown.ParamName);
        AssertNothingReported();
        _plan.Verify();
    }

    [Fact]
    public void TheConstructor_RefusesNullParts()
    {
        Assert.Equal("storage", Assert.Throws<ArgumentNullException>(
            () => new ReportingBrowserStorage(null!, _condition, _logger)).ParamName);
        Assert.Equal("condition", Assert.Throws<ArgumentNullException>(
            () => new ReportingBrowserStorage(Storage, null!, _logger)).ParamName);
        Assert.Equal("logger", Assert.Throws<ArgumentNullException>(
            () => new ReportingBrowserStorage(Storage, _condition, null!)).ParamName);
    }

    private void ExpectRefused(Operation operation)
    {
        switch (operation)
        {
            case Operation.Read: _plan.ExpectRead(Area, Key, BrowserStorageReadAnswer.Refused); break;
            case Operation.Write: _plan.ExpectWrite(Area, Key, Value, BrowserStorageWriteAnswer.Refused); break;
            case Operation.Remove: _plan.ExpectRemove(Area, Key, BrowserStorageWriteAnswer.Refused); break;
        }
    }

    private void AssertNothingReported()
    {
        Assert.False(_condition.HasOccurred);
        Assert.Empty(_logger.Entries);
    }

    private static string OperationWord(Operation operation) => operation switch
    {
        Operation.Read => "read",
        Operation.Write => "write",
        Operation.Remove => "removal",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    // The operation is started synchronously, so an argument error throws from
    // this call itself, as it does from the access.
    private static Task<Outcome> Run(
        ReportingBrowserStorage access, Operation operation, string consequence = Consequence, string key = Key)
    {
        switch (operation)
        {
            case Operation.Read:
                var read = access.ReadAsync(Area, key, consequence);
                return Complete(read, r => new Outcome(r, r.IsRefused, r.Refusal));
            case Operation.Write:
                var written = access.WriteAsync(Area, key, Value, consequence);
                return Complete(written, w => new Outcome(w, w.IsRefused, w.Refusal));
            case Operation.Remove:
                var removed = access.RemoveAsync(Area, key, consequence);
                return Complete(removed, w => new Outcome(w, w.IsRefused, w.Refusal));
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }

        static async Task<Outcome> Complete<T>(Task<T> task, Func<T, Outcome> read) => read(await task);
    }

    private sealed record Outcome(object Result, bool IsRefused, JSException? Refusal);

    // Static rendering's runtime, in miniature: every interop call throws an
    // InvalidOperationException, which is not the browser's answer.
    private sealed class NoInteropRuntime : IJSRuntime
    {
        public InvalidOperationException Failure { get; } = new("JavaScript interop calls cannot be issued.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw Failure;

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) => throw Failure;
    }
}
