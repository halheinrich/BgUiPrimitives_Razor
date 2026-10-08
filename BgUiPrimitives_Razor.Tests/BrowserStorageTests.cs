using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace BgUiPrimitives_Razor.Tests;

// Pins for the access policy Hal ruled on halheinrich/backgammon#374
// (2026-10-08): every requested operation is attempted, with no state carried
// between calls; a read answers stored, absent or refused, and a write or a
// removal answers succeeded or refused; only the browser's refusal — a
// JSException — is caught, and it is carried as raised.
//
// Driven through bUnit's JS interop in strict mode. Each browser answer is
// planned for the exact identifier and arguments an operation must send, and
// a call nobody planned fails the test, so "one call per operation, and no
// read before a write" holds in every test here, not only in the ones that
// name it. The wire is restated below (StorageObject), never taken from the
// implementation: a test that asked the code under test which identifier to
// expect would agree with whatever it did. Nor is it taken from
// BrowserStoragePlan, the test-support planner that restates the same wire
// for consumers: these tests are the independent statement of the protocol
// that the planner is held to, so they plan with bUnit's own Setup and
// SetupVoid and never through the planner. (The planner offers no
// identifier to take; TestSupportPostureTests pins that.)
public class BrowserStorageTests : BunitContext
{
    private const string Key = "bg.test.key";
    private const string Value = "stored value";

    public BrowserStorageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        Services.AddBrowserStorage();
    }

    /// <summary>The three operations, for the tests that hold for each.</summary>
    public enum Operation { Read, Write, Remove }

    // Resolved through the registration a host makes, over bUnit's runtime.
    private BrowserStorage Storage => Services.GetRequiredService<BrowserStorage>();

    public static TheoryData<BrowserStorageArea> Areas =>
        new(Enum.GetValues<BrowserStorageArea>());

    public static TheoryData<Operation> Operations =>
        new(Enum.GetValues<Operation>());

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

    // Every refused call, followed by every call: the same operation on the
    // same key, a different operation, the other area.
    public static TheoryData<Operation, BrowserStorageArea, Operation, BrowserStorageArea> EveryRefusalThenEveryCall
    {
        get
        {
            var data = new TheoryData<Operation, BrowserStorageArea, Operation, BrowserStorageArea>();
            foreach (var (refusedOperation, refusedArea) in Pairs())
            {
                foreach (var (nextOperation, nextArea) in Pairs())
                {
                    data.Add(refusedOperation, refusedArea, nextOperation, nextArea);
                }
            }

            return data;

            static IEnumerable<(Operation, BrowserStorageArea)> Pairs() =>
                from operation in Enum.GetValues<Operation>()
                from area in Enum.GetValues<BrowserStorageArea>()
                select (operation, area);
        }
    }

    // ---- Read: stored, absent, refused ---------------------------------------

    [Theory]
    [MemberData(nameof(Areas))]
    public async Task Read_AnItem_IsStored_WithItsValue(BrowserStorageArea area)
    {
        JSInterop.Setup<string?>($"{StorageObject(area)}.getItem", Key).SetResult(Value);

        AssertStored(await Storage.ReadAsync(area, Key), Value);
        AssertOnlyCall($"{StorageObject(area)}.getItem", Key);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public async Task Read_NoItem_IsAbsent(BrowserStorageArea area)
    {
        JSInterop.Setup<string?>($"{StorageObject(area)}.getItem", Key).SetResult(null);

        AssertAbsent(await Storage.ReadAsync(area, Key));
        AssertOnlyCall($"{StorageObject(area)}.getItem", Key);
    }

    // The browser's getItem answers "" for an item holding the empty string
    // and null for no item; the two are different facts and stay different.
    [Theory]
    [MemberData(nameof(Areas))]
    public async Task Read_TheEmptyString_IsStored_NotAbsent(BrowserStorageArea area)
    {
        JSInterop.Setup<string?>($"{StorageObject(area)}.getItem", "holds-empty").SetResult("");
        JSInterop.Setup<string?>($"{StorageObject(area)}.getItem", "holds-nothing").SetResult(null);

        AssertStored(await Storage.ReadAsync(area, "holds-empty"), "");
        AssertAbsent(await Storage.ReadAsync(area, "holds-nothing"));
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public async Task Read_Refused_IsRefused_WithTheBrowsersOwnException(BrowserStorageArea area)
    {
        var refusal = BrowserRefusal();
        JSInterop.Setup<string?>($"{StorageObject(area)}.getItem", Key).SetException(refusal);

        AssertRefused(await Storage.ReadAsync(area, Key), refusal);
        AssertOnlyCall($"{StorageObject(area)}.getItem", Key);
    }

    // ---- Write: succeeded, refused -------------------------------------------

    [Theory]
    [MemberData(nameof(Areas))]
    public async Task Write_Accepted_Succeeds(BrowserStorageArea area)
    {
        JSInterop.SetupVoid($"{StorageObject(area)}.setItem", Key, Value).SetVoidResult();

        AssertSucceeded(await Storage.WriteAsync(area, Key, Value));
        AssertOnlyCall($"{StorageObject(area)}.setItem", Key, Value);
    }

    // The empty string is a value like any other: it is written, not turned
    // into a removal.
    [Theory]
    [MemberData(nameof(Areas))]
    public async Task Write_TheEmptyString_IsWrittenAsIs(BrowserStorageArea area)
    {
        JSInterop.SetupVoid($"{StorageObject(area)}.setItem", Key, "").SetVoidResult();

        AssertSucceeded(await Storage.WriteAsync(area, Key, ""));
        AssertOnlyCall($"{StorageObject(area)}.setItem", Key, "");
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public async Task Write_Refused_IsRefused_WithTheBrowsersOwnException(BrowserStorageArea area)
    {
        var refusal = BrowserRefusal();
        JSInterop.SetupVoid($"{StorageObject(area)}.setItem", Key, Value).SetException(refusal);

        AssertRefused(await Storage.WriteAsync(area, Key, Value), refusal);
        AssertOnlyCall($"{StorageObject(area)}.setItem", Key, Value);
    }

    // ---- Remove: succeeded, refused ------------------------------------------

    // Removing a key that holds no item succeeds: removeItem answers nothing
    // either way, and the access neither reads first to find out nor has an
    // "absent" outcome to report. One call, and it is removeItem.
    [Theory]
    [MemberData(nameof(Areas))]
    public async Task Remove_Accepted_Succeeds_InOneCall_WithoutReadingFirst(BrowserStorageArea area)
    {
        JSInterop.SetupVoid($"{StorageObject(area)}.removeItem", Key).SetVoidResult();

        AssertSucceeded(await Storage.RemoveAsync(area, Key));
        AssertOnlyCall($"{StorageObject(area)}.removeItem", Key);
    }

    [Theory]
    [MemberData(nameof(Areas))]
    public async Task Remove_Refused_IsRefused_WithTheBrowsersOwnException(BrowserStorageArea area)
    {
        var refusal = BrowserRefusal();
        JSInterop.SetupVoid($"{StorageObject(area)}.removeItem", Key).SetException(refusal);

        AssertRefused(await Storage.RemoveAsync(area, Key), refusal);
        AssertOnlyCall($"{StorageObject(area)}.removeItem", Key);
    }

    // ---- No latch: a refusal disables nothing --------------------------------

    // The ruled policy, at its broadest: after any refused call, any next call
    // — the same one again, another operation, the other area — is made, and
    // its own answer is what comes back. That includes a refused write
    // followed by a read that succeeds. A latch of any scope (the instance,
    // the area, the operation, the key) fails some case here.
    [Theory]
    [MemberData(nameof(EveryRefusalThenEveryCall))]
    public async Task ARefusal_DisablesNothing_TheNextCallIsMade_AndItsAnswerReturned(
        Operation refusedOperation, BrowserStorageArea refusedArea,
        Operation nextOperation, BrowserStorageArea nextArea)
    {
        var refused = Plan(refusedOperation, refusedArea);
        var refusal = BrowserRefusal();
        refused.Throw(refusal);
        Assert.Same(refusal, await refused.RunAsync(Storage));

        var next = (nextOperation, nextArea) == (refusedOperation, refusedArea)
            ? refused
            : Plan(nextOperation, nextArea);
        next.Accept();
        Assert.Null(await next.RunAsync(Storage));

        Assert.Equal(2, JSInterop.Invocations.Count);
    }

    // And no state the other way round: a refusal after a success is reported
    // as the refusal it is.
    [Theory]
    [MemberData(nameof(EveryOperationInEveryArea))]
    public async Task ASuccess_IsNotCarriedForward_ALaterRefusalIsReported(
        Operation operation, BrowserStorageArea area)
    {
        var planned = Plan(operation, area);
        planned.Accept();
        Assert.Null(await planned.RunAsync(Storage));

        var refusal = BrowserRefusal();
        planned.Throw(refusal);
        Assert.Same(refusal, await planned.RunAsync(Storage));
    }

    // ---- Only the browser's refusal is caught --------------------------------

    // What static rendering's JS runtime throws for any interop call: an
    // InvalidOperationException, not a JSException (measured against the
    // .NET 10 runtime when this type was introduced). Not an answer from the
    // browser, so not a refusal.
    [Theory]
    [MemberData(nameof(EveryOperationInEveryArea))]
    public async Task AnInvalidOperation_Propagates_Unchanged(Operation operation, BrowserStorageArea area)
    {
        var planned = Plan(operation, area);
        var thrown = new InvalidOperationException("JavaScript interop calls cannot be issued now.");
        planned.Throw(thrown);

        Assert.Same(thrown, await Assert.ThrowsAsync<InvalidOperationException>(() => planned.RunAsync(Storage)));
    }

    // A server circuit that has gone. JSDisconnectedException is interop's own
    // exception and still not a JSException: the browser never answered.
    [Theory]
    [MemberData(nameof(EveryOperationInEveryArea))]
    public async Task ADisconnectedCircuit_Propagates_Unchanged(Operation operation, BrowserStorageArea area)
    {
        var planned = Plan(operation, area);
        var thrown = new JSDisconnectedException("The circuit has gone.");
        planned.Throw(thrown);

        Assert.Same(thrown, await Assert.ThrowsAsync<JSDisconnectedException>(() => planned.RunAsync(Storage)));
    }

    // A call that timed out or was cancelled had no answer either.
    [Theory]
    [MemberData(nameof(EveryOperationInEveryArea))]
    public async Task ACancellation_Propagates(Operation operation, BrowserStorageArea area)
    {
        var planned = Plan(operation, area);
        planned.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => planned.RunAsync(Storage));
    }

    // ---- Keys and values -----------------------------------------------------

    // The empty string is a key like any other, in every operation.
    [Theory]
    [MemberData(nameof(EveryOperationInEveryArea))]
    public async Task TheEmptyKey_IsAKeyLikeAnyOther(Operation operation, BrowserStorageArea area)
    {
        var planned = Plan(operation, area, key: "");
        planned.Accept();

        Assert.Null(await planned.RunAsync(Storage));
    }

    // ---- Usage errors: thrown at the call, before any call to the browser ----

    [Theory]
    [MemberData(nameof(Operations))]
    public void AnUndeclaredArea_ThrowsAtTheCall_AndCallsNothing(Operation operation)
    {
        var thrown = ThrowsAtTheCall<ArgumentOutOfRangeException>(
            () => Run(operation, (BrowserStorageArea)(-1), Key, Value));

        Assert.Equal("area", thrown.ParamName);
        Assert.Empty(JSInterop.Invocations);
    }

    [Theory]
    [MemberData(nameof(EveryOperationInEveryArea))]
    public void ANullKey_ThrowsAtTheCall_AndCallsNothing(Operation operation, BrowserStorageArea area)
    {
        var thrown = ThrowsAtTheCall<ArgumentNullException>(() => Run(operation, area, null!, Value));

        Assert.Equal("key", thrown.ParamName);
        Assert.Empty(JSInterop.Invocations);
    }

    // Web Storage would store null as the string "null". Storing nothing is a
    // removal, and says so.
    [Theory]
    [MemberData(nameof(Areas))]
    public void ANullValue_ThrowsAtTheCall_AndCallsNothing(BrowserStorageArea area)
    {
        var thrown = ThrowsAtTheCall<ArgumentNullException>(() => Storage.WriteAsync(area, Key, null!));

        Assert.Equal("value", thrown.ParamName);
        Assert.Empty(JSInterop.Invocations);
    }

    [Fact]
    public void ANullRuntime_Throws()
    {
        var thrown = Assert.Throws<ArgumentNullException>(() => new BrowserStorage(null!));

        Assert.Equal("js", thrown.ParamName);
    }

    // ---- Helpers -------------------------------------------------------------

    // The browser object each area addresses: the wire, restated.
    private static string StorageObject(BrowserStorageArea area) => area switch
    {
        BrowserStorageArea.Local => "localStorage",
        BrowserStorageArea.Session => "sessionStorage",
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, null),
    };

    // What a browser that blocks storage raises, as it reaches .NET.
    private static JSException BrowserRefusal() =>
        new("SecurityError: Failed to read the 'localStorage' property from 'Window': Access is denied for this document.");

    private Task Run(Operation operation, BrowserStorageArea area, string key, string value) => operation switch
    {
        Operation.Read => Storage.ReadAsync(area, key),
        Operation.Write => Storage.WriteAsync(area, key, value),
        Operation.Remove => Storage.RemoveAsync(area, key),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
    };

    // A usage error throws from the call itself, before a task exists: the
    // task-based asynchronous pattern's rule, and what the API documents.
    private static TException ThrowsAtTheCall<TException>(Func<Task> call)
        where TException : Exception
    {
        Task? returned = null;
        var thrown = Record.Exception(() => { returned = call(); });

        Assert.Null(returned);
        return Assert.IsType<TException>(thrown);
    }

    private void AssertOnlyCall(string identifier, params object?[] arguments)
    {
        var call = Assert.Single(JSInterop.Invocations);
        Assert.Equal(identifier, call.Identifier);
        Assert.Equal(arguments, call.Arguments);
    }

    private static void AssertStored(BrowserStorageReadResult read, string value)
    {
        Assert.Equal((true, false, false), (read.IsStored, read.IsAbsent, read.IsRefused));
        Assert.Equal(value, read.Value);
        Assert.Null(read.Refusal);
    }

    private static void AssertAbsent(BrowserStorageReadResult read)
    {
        Assert.Equal((false, true, false), (read.IsStored, read.IsAbsent, read.IsRefused));
        Assert.Null(read.Value);
        Assert.Null(read.Refusal);
    }

    private static void AssertRefused(BrowserStorageReadResult read, JSException refusal)
    {
        Assert.Equal((false, false, true), (read.IsStored, read.IsAbsent, read.IsRefused));
        Assert.Null(read.Value);
        Assert.Same(refusal, read.Refusal);
    }

    private static void AssertSucceeded(BrowserStorageWriteResult written)
    {
        Assert.Equal((true, false), (written.Succeeded, written.IsRefused));
        Assert.Null(written.Refusal);
    }

    private static void AssertRefused(BrowserStorageWriteResult written, JSException refusal)
    {
        Assert.Equal((false, true), (written.Succeeded, written.IsRefused));
        Assert.Same(refusal, written.Refusal);
    }

    // Plans the one call an operation must make, with its exact identifier
    // and arguments; the browser's answer is set on what comes back.
    private PlannedCall Plan(Operation operation, BrowserStorageArea area, string key = Key) => operation switch
    {
        Operation.Read => new(operation, area, key,
            JSInterop.Setup<string?>($"{StorageObject(area)}.getItem", key), write: null),
        Operation.Write => new(operation, area, key,
            read: null, JSInterop.SetupVoid($"{StorageObject(area)}.setItem", key, Value)),
        Operation.Remove => new(operation, area, key,
            read: null, JSInterop.SetupVoid($"{StorageObject(area)}.removeItem", key)),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
    };

    // One operation's planned call, whichever result type it has, so a test
    // can hold for all three. The browser's answer can be set again between
    // runs; each run's own answer is what it reports.
    private sealed class PlannedCall(
        Operation operation,
        BrowserStorageArea area,
        string key,
        JSRuntimeInvocationHandler<string?>? read,
        JSRuntimeInvocationHandler? write)
    {
        // The browser accepts: a read finds Value stored, a write lands.
        public void Accept()
        {
            read?.SetResult(Value);
            write?.SetVoidResult();
        }

        public void Throw(Exception exception)
        {
            read?.SetException(exception);
            write?.SetException(exception);
        }

        public void Cancel()
        {
            read?.SetCanceled();
            write?.SetCanceled();
        }

        // Runs the operation once. Returns the refusal it reports, or null
        // when it reports the browser's acceptance (asserting that the
        // accepted result says exactly that).
        public async Task<JSException?> RunAsync(BrowserStorage storage)
        {
            switch (operation)
            {
                case Operation.Read:
                    var found = await storage.ReadAsync(area, key);
                    if (found.IsRefused)
                    {
                        AssertRefused(found, found.Refusal);
                        return found.Refusal;
                    }

                    AssertStored(found, Value);
                    return null;

                case Operation.Write:
                    return Reported(await storage.WriteAsync(area, key, Value));

                case Operation.Remove:
                    return Reported(await storage.RemoveAsync(area, key));

                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }

            static JSException? Reported(BrowserStorageWriteResult written)
            {
                if (written.IsRefused)
                {
                    AssertRefused(written, written.Refusal);
                    return written.Refusal;
                }

                AssertSucceeded(written);
                return null;
            }
        }
    }
}
