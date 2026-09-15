namespace Lpubsppop01.EBookBuilder.Core;

/// <summary>Progress report. Used by the UI side to build its messages.</summary>
public readonly record struct PageProgress(int Done, int Total)
{
    /// <summary>Progress percentage from 0 to 100.</summary>
    public int Percentage => Total <= 0 ? 100 : (int)Math.Floor((double)Done / Total * 100);
}
