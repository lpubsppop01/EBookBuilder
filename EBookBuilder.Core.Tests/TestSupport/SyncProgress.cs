namespace Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;

/// <summary>Collects progress reports and runs inline.</summary>
/// <remarks>
/// <see cref="Progress{T}"/> hands its callbacks to the captured synchronization context
/// asynchronously, so counting the reports as soon as the call returns is a race.
/// This implementation reports on the calling thread instead, which makes
/// assertions about the number of reports deterministic.
/// </remarks>
public sealed class SyncProgress<T> : IProgress<T>
{
    readonly List<T> m_Reports = [];

    /// <summary>The reports collected so far, in the order they arrived.</summary>
    public IReadOnlyList<T> Reports => m_Reports;

    public void Report(T value) => m_Reports.Add(value);
}
