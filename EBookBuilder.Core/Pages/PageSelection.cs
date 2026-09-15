namespace Lpubsppop01.EBookBuilder.Core.Pages;

/// <summary>Identifying the page selected as the target of an operation.</summary>
public static class PageSelection
{
    /// <summary>Returns the position when exactly one item is selected. Otherwise -1.</summary>
    /// <remarks>
    /// <para>
    /// Duplicate, move, delete and crop each need the single page they act on to be unambiguous.
    /// With 0 or 2 or more selected the operation cannot be performed, so -1 is returned.
    /// </para>
    /// <para>
    /// Writing this with <c>Enumerable.SingleOrDefault</c> would throw when 2 or more are
    /// selected (it crashes when everything is checked). Here the decision is made by counting.
    /// </para>
    /// </remarks>
    public static int IndexOfSingleChecked<T>(IReadOnlyList<T> items, Func<T, bool> isChecked)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(isChecked);

        var foundIndex = -1;
        for (var i = 0; i < items.Count; ++i)
        {
            if (!isChecked(items[i])) continue;

            // As soon as a second one is found, it is settled that the target is not unambiguous.
            if (foundIndex >= 0) return -1;
            foundIndex = i;
        }

        return foundIndex;
    }
}
