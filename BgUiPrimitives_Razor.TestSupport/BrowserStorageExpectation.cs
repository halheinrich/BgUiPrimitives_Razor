using System.Diagnostics;
using Microsoft.JSInterop.Infrastructure;

namespace BgUiPrimitives_Razor.TestSupport;

/// <summary>
/// One storage call, or a stated number of identical ones, that a
/// <see cref="BrowserStoragePlan"/> expects, with the answer it gives each.
/// Returned by the plan's <c>Expect…</c> methods, for a test to name in
/// <see cref="BrowserStoragePlan.RequireOrder"/>.
/// </summary>
/// <remarks>
/// Only a plan creates one. The held forms, whose answer the test gives when
/// it releases the call, are <see cref="BrowserStorageHeldRead"/> and
/// <see cref="BrowserStorageHeldWrite"/>.
/// </remarks>
public class BrowserStorageExpectation
{
    private readonly BrowserStorageReadAnswer? _readAnswer;
    private readonly BrowserStorageWriteAnswer? _writeAnswer;
    private readonly List<int> _callNumbers = [];

    // An expectation answered at the call, by the answer declared with it.
    internal BrowserStorageExpectation(
        BrowserStoragePlan plan,
        StorageCall call,
        int times,
        BrowserStorageReadAnswer? readAnswer,
        BrowserStorageWriteAnswer? writeAnswer)
    {
        Plan = plan;
        Call = call;
        Times = times;
        _readAnswer = readAnswer;
        _writeAnswer = writeAnswer;
    }

    // A held expectation: one call, answered when the test releases it.
    internal BrowserStorageExpectation(BrowserStoragePlan plan, StorageCall call)
        : this(plan, call, times: 1, readAnswer: null, writeAnswer: null)
    {
    }

    internal BrowserStoragePlan Plan { get; }

    internal StorageCall Call { get; }

    internal int Times { get; }

    // The numbers, in the plan's call log, of the calls matched to this
    // expectation. Read and written under the plan's lock.
    internal IReadOnlyList<int> CallNumbers => _callNumbers;

    internal bool IsSpent => _callNumbers.Count >= Times;

    /// <summary>Describes the expectation, for a failure message.</summary>
    /// <returns>The description.</returns>
    public override string ToString()
    {
        var answer = (object?)_readAnswer ?? _writeAnswer;
        var times = Times == 1 ? "" : $", {Times} times";
        return $"{Call}, answered {answer}{times}";
    }

    internal void Match(int callNumber) => _callNumbers.Add(callNumber);

    // The answer to one matched call, as interop would return it. The plan
    // matches a read only to a read expectation and a write or removal only
    // to one of its own operation, so the answer of the other kind is never
    // asked for.
    internal virtual Task<string?> AnswerRead()
    {
        var call = new TaskCompletionSource<string?>();
        (_readAnswer ?? throw new UnreachableException()).Answer(call, Call.ToString());
        return call.Task;
    }

    internal virtual Task<IJSVoidResult> AnswerWrite()
    {
        var call = new TaskCompletionSource<IJSVoidResult>();
        (_writeAnswer ?? throw new UnreachableException()).Answer(call, Call.ToString());
        return call.Task;
    }
}
