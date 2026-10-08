using System.Runtime.CompilerServices;
using Bunit;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace BgUiPrimitives_Razor.TestSupport;

/// <summary>
/// The browser storage a test plans, in <see cref="BrowserStorage"/>'s own
/// vocabulary: which reads, writes and removals the code under test makes —
/// area, key, and for a write the value — how the browser answers each, and
/// afterwards whether the calls made were exactly those. The one place
/// outside the library that knows how <see cref="BrowserStorage"/> reaches
/// the browser, so that no consumer test names an interop identifier.
/// </summary>
/// <remarks>
/// <para>
/// <b>The real accessor runs.</b> A plan answers the interop calls the real
/// <see cref="BrowserStorage"/> makes through bUnit's JS runtime; nothing
/// stands in for the accessor. A test registers
/// <see cref="BrowserStorage"/> as a host does, and the results its code sees
/// are the accessor's own.
/// </para>
/// <para>
/// <b>Every storage call is the plan's.</b> Once a plan is on a
/// <see cref="BunitJSInterop"/>, it answers every storage call made there,
/// in either <see cref="JSRuntimeMode"/>. A call it does not expect fails at
/// the call with <see cref="BrowserStorageAssertionException"/>, which the
/// accessor does not catch; and because code under test may catch it, the
/// call is also recorded, and <see cref="Verify"/> reports it. Nothing else
/// on the <see cref="BunitJSInterop"/> is touched: other plans answer their
/// own calls, and the invocation record stays whole.
/// </para>
/// <para>
/// <b>Calls are matched, not sequenced.</b> A call is answered by the
/// earliest declared expectation for the same operation, area, key and —
/// for a write — value that it has not used up. So successive answers for
/// one call are declared in the order they are to be given, while calls to
/// different keys, areas or values may arrive in any order. Where the order
/// matters to the test, it says so with <see cref="RequireOrder"/>.
/// </para>
/// <para>
/// <b>An independent reader, by design.</b> How each operation reaches the
/// browser is restated here rather than shared with
/// <see cref="BrowserStorage"/> through internals: a planner that took its
/// identifiers from the implementation would agree with whatever the
/// implementation did. This repository's tests hold the two together by
/// running the real accessor through this type.
/// </para>
/// </remarks>
public sealed class BrowserStoragePlan
{
    // The wire, restated: the browser object each area is and the method each
    // operation calls, as BrowserStorage's contract states them. A call to any
    // of these is the plan's to answer.
    private static readonly Dictionary<string, (BrowserStorageArea Area, StorageOperation Operation)> Wire =
        new(StringComparer.Ordinal)
        {
            ["localStorage.getItem"] = (BrowserStorageArea.Local, StorageOperation.Read),
            ["localStorage.setItem"] = (BrowserStorageArea.Local, StorageOperation.Write),
            ["localStorage.removeItem"] = (BrowserStorageArea.Local, StorageOperation.Remove),
            ["sessionStorage.getItem"] = (BrowserStorageArea.Session, StorageOperation.Read),
            ["sessionStorage.setItem"] = (BrowserStorageArea.Session, StorageOperation.Write),
            ["sessionStorage.removeItem"] = (BrowserStorageArea.Session, StorageOperation.Remove),
        };

    // One plan per runtime: a second would answer every storage call, and the
    // first would expect calls it never sees.
    private static readonly ConditionalWeakTable<BunitJSInterop, BrowserStoragePlan> Plans = new();

    private readonly Lock _gate = new();
    private readonly BunitJSInterop _jsInterop;
    private readonly List<BrowserStorageExpectation> _expectations = [];
    private readonly List<LoggedCall> _calls = [];
    private readonly List<BrowserStorageExpectation[]> _orders = [];

    private BrowserStoragePlan(BunitJSInterop jsInterop) => _jsInterop = jsInterop;

    /// <summary>
    /// Puts a plan on <paramref name="jsInterop"/>, the test's bUnit JS
    /// interop, so that it answers every storage call the code under test
    /// makes there. The plan starts empty: until something is expected, every
    /// storage call is unexpected.
    /// </summary>
    /// <param name="jsInterop">The test context's <c>JSInterop</c>.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="jsInterop"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="jsInterop"/> already has a plan.</exception>
    public static BrowserStoragePlan On(BunitJSInterop jsInterop)
    {
        ArgumentNullException.ThrowIfNull(jsInterop);

        var plan = new BrowserStoragePlan(jsInterop);
        if (!Plans.TryAdd(jsInterop, plan))
        {
            throw new InvalidOperationException(
                $"This JSInterop already has a {nameof(BrowserStoragePlan)}. A test has one plan, which "
                + "answers every storage call; declare everything on it.");
        }

        jsInterop.AddInvocationHandler(new ReadHandler(plan));
        jsInterop.AddInvocationHandler(new WriteHandler(plan));
        return plan;
    }

    /// <summary>
    /// Expects a read of <paramref name="key"/> in <paramref name="area"/>,
    /// answered at once with <paramref name="answer"/>.
    /// </summary>
    /// <param name="area">The area read.</param>
    /// <param name="key">The key read. Any string, the empty string included.</param>
    /// <param name="answer">How the browser answers.</param>
    /// <param name="times">How many such reads are expected, each answered alike.</param>
    /// <returns>The expectation, to name in <see cref="RequireOrder"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="area"/> is not a declared area, or
    /// <paramref name="times"/> is less than one.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="answer"/> is <see langword="null"/>.
    /// </exception>
    public BrowserStorageExpectation ExpectRead(
        BrowserStorageArea area, string key, BrowserStorageReadAnswer answer, int times = 1)
    {
        var call = StorageCall.Read(Declared(area), NotNull(key));
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentOutOfRangeException.ThrowIfLessThan(times, 1);

        return Add(new BrowserStorageExpectation(this, call, times, answer, writeAnswer: null));
    }

    /// <summary>
    /// Expects a write of <paramref name="value"/> under
    /// <paramref name="key"/> in <paramref name="area"/>, answered at once
    /// with <paramref name="answer"/>.
    /// </summary>
    /// <param name="area">The area written.</param>
    /// <param name="key">The key written. Any string, the empty string included.</param>
    /// <param name="value">The value the write must carry. Any string, the empty string included.</param>
    /// <param name="answer">How the browser answers.</param>
    /// <param name="times">How many such writes are expected, each answered alike.</param>
    /// <returns>The expectation, to name in <see cref="RequireOrder"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="area"/> is not a declared area, or
    /// <paramref name="times"/> is less than one.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/>, <paramref name="value"/> or
    /// <paramref name="answer"/> is <see langword="null"/>.
    /// </exception>
    public BrowserStorageExpectation ExpectWrite(
        BrowserStorageArea area, string key, string value, BrowserStorageWriteAnswer answer, int times = 1)
    {
        var call = StorageCall.Write(Declared(area), NotNull(key), NotNull(value));
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentOutOfRangeException.ThrowIfLessThan(times, 1);

        return Add(new BrowserStorageExpectation(this, call, times, readAnswer: null, answer));
    }

    /// <summary>
    /// Expects a removal of <paramref name="key"/> from
    /// <paramref name="area"/>, answered at once with <paramref name="answer"/>.
    /// </summary>
    /// <param name="area">The area written.</param>
    /// <param name="key">The key removed. Any string, the empty string included.</param>
    /// <param name="answer">How the browser answers.</param>
    /// <param name="times">How many such removals are expected, each answered alike.</param>
    /// <returns>The expectation, to name in <see cref="RequireOrder"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="area"/> is not a declared area, or
    /// <paramref name="times"/> is less than one.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="answer"/> is <see langword="null"/>.
    /// </exception>
    public BrowserStorageExpectation ExpectRemove(
        BrowserStorageArea area, string key, BrowserStorageWriteAnswer answer, int times = 1)
    {
        var call = StorageCall.Remove(Declared(area), NotNull(key));
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentOutOfRangeException.ThrowIfLessThan(times, 1);

        return Add(new BrowserStorageExpectation(this, call, times, readAnswer: null, answer));
    }

    /// <summary>
    /// Expects one read of <paramref name="key"/> in <paramref name="area"/>
    /// and holds it: the read stays pending until the test releases it.
    /// </summary>
    /// <param name="area">The area read.</param>
    /// <param name="key">The key read. Any string, the empty string included.</param>
    /// <returns>The held read, to observe and release.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="area"/> is not a declared area.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public BrowserStorageHeldRead ExpectHeldRead(BrowserStorageArea area, string key) =>
        Add(new BrowserStorageHeldRead(this, StorageCall.Read(Declared(area), NotNull(key))));

    /// <summary>
    /// Expects one write of <paramref name="value"/> under
    /// <paramref name="key"/> in <paramref name="area"/> and holds it: the
    /// write stays pending until the test releases it.
    /// </summary>
    /// <param name="area">The area written.</param>
    /// <param name="key">The key written. Any string, the empty string included.</param>
    /// <param name="value">The value the write must carry. Any string, the empty string included.</param>
    /// <returns>The held write, to observe and release.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="area"/> is not a declared area.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public BrowserStorageHeldWrite ExpectHeldWrite(BrowserStorageArea area, string key, string value) =>
        Add(new BrowserStorageHeldWrite(this, StorageCall.Write(Declared(area), NotNull(key), NotNull(value))));

    /// <summary>
    /// Expects one removal of <paramref name="key"/> from
    /// <paramref name="area"/> and holds it: the removal stays pending until
    /// the test releases it.
    /// </summary>
    /// <param name="area">The area written.</param>
    /// <param name="key">The key removed. Any string, the empty string included.</param>
    /// <returns>The held removal, to observe and release.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="area"/> is not a declared area.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public BrowserStorageHeldWrite ExpectHeldRemove(BrowserStorageArea area, string key) =>
        Add(new BrowserStorageHeldWrite(this, StorageCall.Remove(Declared(area), NotNull(key))));

    /// <summary>
    /// Requires the calls matched to <paramref name="sequence"/> to have been
    /// made in that order: every call of an earlier expectation before every
    /// call of a later one. Checked by <see cref="Verify"/>. Expectations not
    /// named in a sequence keep no order.
    /// </summary>
    /// <param name="sequence">Two or more distinct expectations of this plan, in the required order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sequence"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="sequence"/> has fewer than two expectations, holds a
    /// <see langword="null"/>, names one twice, or names one of another plan.
    /// </exception>
    public void RequireOrder(params BrowserStorageExpectation[] sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        if (sequence.Length < 2)
        {
            throw new ArgumentException("An order needs two or more expectations.", nameof(sequence));
        }

        if (sequence.Any(expectation => expectation is null || expectation.Plan != this))
        {
            throw new ArgumentException("Every expectation in an order must be one of this plan's.", nameof(sequence));
        }

        if (sequence.Distinct().Count() != sequence.Length)
        {
            throw new ArgumentException("An order names each expectation once.", nameof(sequence));
        }

        lock (_gate)
        {
            _orders.Add([.. sequence]);
        }
    }

    /// <summary>
    /// Asserts that the storage calls made so far are exactly the ones
    /// planned: none the plan did not expect (including one more of a call
    /// it expected), every expectation made as many times as it says, and
    /// every required order kept. A held call that was reached counts as
    /// made, released or not.
    /// </summary>
    /// <exception cref="BrowserStorageAssertionException">
    /// The calls differ from the plan; the message states every difference,
    /// and the calls made, in order.
    /// </exception>
    public void Verify()
    {
        lock (_gate)
        {
            var problems = new List<string>();

            problems.AddRange(_calls.Where(c => c.Problem is not null).Select(c => c.Problem!));

            foreach (var expectation in _expectations.Where(e => !e.IsSpent))
            {
                problems.Add(expectation.CallNumbers.Count == 0
                    ? $"Expected and not made: {expectation}."
                    : $"Made {expectation.CallNumbers.Count} of the {expectation.Times} times expected: {expectation}.");
            }

            foreach (var order in _orders)
            {
                problems.AddRange(BrokenOrder(order));
            }

            problems.AddRange(AnsweredElsewhere());

            if (problems.Count > 0)
            {
                throw new BrowserStorageAssertionException(
                    "The browser storage calls are not what the test planned:"
                    + string.Concat(problems.Select(p => Environment.NewLine + "  - " + p))
                    + Environment.NewLine + "Storage calls made, in order:"
                    + (_calls.Count == 0
                        ? Environment.NewLine + "  (none)"
                        : string.Concat(_calls.Select(c => Environment.NewLine + $"  {c.Number}. {c.Description}"
                            + (c.Problem is null ? "" : " (not expected)")))));
            }
        }
    }

    // What a refused call raises: a JSException, as a browser's refusal
    // reaches .NET, new for each call.
    internal static JSException Refusal(string description) =>
        new($"The test's browser storage plan refused {description}.");

    // ---- Answering -------------------------------------------------------

    private static bool IsStorageCall(JSRuntimeInvocation invocation) => Wire.ContainsKey(invocation.Identifier);

    private Task<TResult> Answer<TResult>(
        JSRuntimeInvocation invocation, Func<BrowserStorageExpectation, Task<TResult>> answer)
    {
        lock (_gate)
        {
            var number = _calls.Count + 1;

            if (!TryRead(invocation, out var call))
            {
                return Unexpected<TResult>(
                    number, invocation, $"a call BrowserStorage never makes, {Spelled(invocation)}",
                    "it is none of the three storage operations as BrowserStorage sends them");
            }

            var expectation = _expectations.FirstOrDefault(e => e.Call == call && !e.IsSpent);
            if (expectation is null)
            {
                return Unexpected<TResult>(number, invocation, call.ToString(), WhyUnexpected(call));
            }

            expectation.Match(number);
            _calls.Add(new LoggedCall(number, invocation, call.ToString(), Problem: null));
            return answer(expectation);
        }
    }

    private Task<TResult> Unexpected<TResult>(
        int number, JSRuntimeInvocation invocation, string description, string why)
    {
        var problem = $"Call {number}, {description}, was not expected: {why}.";
        _calls.Add(new LoggedCall(number, invocation, description, problem));
        return Task.FromException<TResult>(new BrowserStorageAssertionException(problem));
    }

    private string WhyUnexpected(StorageCall call)
    {
        var expected = _expectations.Where(e => e.Call == call).Sum(e => e.Times);
        if (expected > 0)
        {
            return $"it is one more than the {expected} the plan expects";
        }

        var open = _expectations.Where(e => !e.IsSpent && e.Call.SameItemAs(call)).ToList();
        return open.Count == 0
            ? $"the plan expects nothing more for {call.Item}"
            : $"for {call.Item} the plan expects {string.Join("; ", open)}";
    }

    // Reads an invocation as one of the accessor's calls: the identifier names
    // the area and the operation; a read returns the item's string, a write or
    // removal returns nothing; the arguments are the key, and for a write the
    // value. Anything else is no call BrowserStorage makes.
    private static bool TryRead(JSRuntimeInvocation invocation, out StorageCall call)
    {
        call = default;

        if (!Wire.TryGetValue(invocation.Identifier, out var target)
            || invocation.IsVoidResultInvocation == (target.Operation == StorageOperation.Read))
        {
            return false;
        }

        switch (target.Operation, invocation.Arguments)
        {
            case (StorageOperation.Read, [string key]):
                call = StorageCall.Read(target.Area, key);
                return true;
            case (StorageOperation.Write, [string key, string value]):
                call = StorageCall.Write(target.Area, key, value);
                return true;
            case (StorageOperation.Remove, [string key]):
                call = StorageCall.Remove(target.Area, key);
                return true;
            default:
                return false;
        }
    }

    // ---- Verifying -------------------------------------------------------

    private static IEnumerable<string> BrokenOrder(BrowserStorageExpectation[] order)
    {
        for (var earlier = 0; earlier < order.Length; earlier++)
        {
            for (var later = earlier + 1; later < order.Length; later++)
            {
                var (first, second) = (order[earlier], order[later]);

                // A call not made at all is reported as not made.
                if (first.CallNumbers.Count == 0 || second.CallNumbers.Count == 0)
                {
                    continue;
                }

                var lastOfFirst = first.CallNumbers.Max();
                var firstOfSecond = second.CallNumbers.Min();
                if (lastOfFirst > firstOfSecond)
                {
                    yield return $"Out of the required order: call {firstOfSecond}, {second}, came before "
                        + $"call {lastOfFirst}, {first}.";
                }
            }
        }
    }

    // Every storage call bUnit recorded must be one this plan answered. One
    // that is not was answered by a handler of the test's own that spells the
    // identifier, or reached the runtime with a result type no storage
    // operation has.
    private IEnumerable<string> AnsweredElsewhere()
    {
        var answered = _calls.Select(c => c.Invocation).ToList();

        foreach (var identifier in Wire.Keys)
        {
            foreach (var invocation in _jsInterop.Invocations[identifier])
            {
                if (!answered.Remove(invocation))
                {
                    yield return $"A storage call the plan did not answer, {Spelled(invocation)}: another "
                        + "handler on the JSInterop answered it, or nothing did.";
                }
            }
        }
    }

    // ---- Declaring -------------------------------------------------------

    private T Add<T>(T expectation)
        where T : BrowserStorageExpectation
    {
        lock (_gate)
        {
            _expectations.Add(expectation);
        }

        return expectation;
    }

    private static BrowserStorageArea Declared(BrowserStorageArea area, [CallerArgumentExpression(nameof(area))] string? name = null) =>
        Wire.Values.Any(target => target.Area == area)
            ? area
            : throw new ArgumentOutOfRangeException(name, area, $"{nameof(BrowserStorageArea)} has no storage object for the value {area}.");

    private static string NotNull(string? argument, [CallerArgumentExpression(nameof(argument))] string? name = null)
    {
        ArgumentNullException.ThrowIfNull(argument, name);
        return argument;
    }

    private static string Spelled(JSRuntimeInvocation invocation) =>
        $"{invocation.InvocationMethodName}(\"{invocation.Identifier}\""
        + string.Concat(invocation.Arguments.Select(a => ", " + (a is string s ? $"\"{s}\"" : a?.ToString() ?? "null")))
        + ")";

    // One storage call the plan saw, in the order it saw them: answered by an
    // expectation (Problem null), or unexpected (Problem says why).
    private sealed record LoggedCall(int Number, JSRuntimeInvocation Invocation, string Description, string? Problem);

    // The plan's handlers. Neither calls the base HandleAsync, which bUnit
    // asks overrides to call so the handler's own Invocations is kept: that
    // base answers from one shared pending task and fails it after bUnit's
    // DefaultWaitTimeout, and these answer each call from the plan. bUnit's
    // own invocation record, the one tests read, is kept by the runtime
    // before any handler runs.
    private sealed class ReadHandler(BrowserStoragePlan plan)
        : JSRuntimeInvocationHandlerBase<string?>(IsStorageCall, isCatchAllHandler: false)
    {
        protected override Task<string?> HandleAsync(JSRuntimeInvocation invocation) =>
            plan.Answer(invocation, expectation => expectation.AnswerRead());
    }

    private sealed class WriteHandler(BrowserStoragePlan plan)
        : JSRuntimeInvocationHandlerBase<IJSVoidResult>(IsStorageCall, isCatchAllHandler: false)
    {
        public override bool IsVoidResultHandler => true;

        protected override Task<IJSVoidResult> HandleAsync(JSRuntimeInvocation invocation) =>
            plan.Answer(invocation, expectation => expectation.AnswerWrite());
    }
}
