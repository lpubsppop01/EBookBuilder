using Lpubsppop01.EBookBuilder.Core.Pages;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Pages;

/// <summary>Verification of identifying the operation target.</summary>
/// <remarks>
/// The key point is that it does not throw when two or more pages are selected.
/// This prevents a regression of the bug that crashed when everything was checked.
/// </remarks>
public class PageSelectionTests
{
    static int IndexOf(params bool[] states) =>
        PageSelection.IndexOfSingleChecked(states, state => state);

    [Fact]
    public void NothingSelectedReturnsNoMatch()
    {
        Assert.Equal(-1, IndexOf());
        Assert.Equal(-1, IndexOf(false));
        Assert.Equal(-1, IndexOf(false, false, false));
    }

    [Fact]
    public void SingleSelectionReturnsItsIndex()
    {
        Assert.Equal(0, IndexOf(true));
        Assert.Equal(0, IndexOf(true, false, false));
        Assert.Equal(1, IndexOf(false, true, false));
        Assert.Equal(2, IndexOf(false, false, true));
    }

    [Fact]
    public void TwoOrMoreSelectionsReturnNoMatchWithoutThrowing()
    {
        // Using Enumerable.SingleOrDefault here would throw InvalidOperationException.
        Assert.Equal(-1, IndexOf(true, true));
        Assert.Equal(-1, IndexOf(false, true, true));
        Assert.Equal(-1, IndexOf(true, false, true));
    }

    [Fact]
    public void AllSelectedDoesNotThrow()
    {
        // This is exactly the code path that crashed when everything was checked.
        Assert.Equal(-1, IndexOf(true, true, true, true, true));

        var many = Enumerable.Repeat(true, 1000).ToArray();
        Assert.Equal(-1, IndexOf(many));
    }

    [Fact]
    public void FirstAndLastSelectedReturnsNoMatch()
    {
        Assert.Equal(-1, IndexOf(true, false, false, false, true));
    }

    [Fact]
    public void EmptyListReturnsNoMatch()
    {
        Assert.Equal(-1, PageSelection.IndexOfSingleChecked(Array.Empty<int>(), _ => true));
    }

    [Fact]
    public void PredicateIsAppliedPerElement()
    {
        var items = new[] { "a", "b", "c" };
        Assert.Equal(1, PageSelection.IndexOfSingleChecked(items, item => item == "b"));
        Assert.Equal(-1, PageSelection.IndexOfSingleChecked(items, _ => true));
        Assert.Equal(-1, PageSelection.IndexOfSingleChecked(items, _ => false));
    }

    [Fact]
    public void NullArgumentThrows()
    {
        Assert.Throws<ArgumentNullException>(
            () => PageSelection.IndexOfSingleChecked<object>(null!, _ => true));
        Assert.Throws<ArgumentNullException>(
            () => PageSelection.IndexOfSingleChecked(new[] { 1 }, null!));
    }
}
