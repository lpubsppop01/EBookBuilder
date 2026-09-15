namespace Lpubsppop01.EBookBuilder.App.ViewModels;

/// <summary>One page as it appears in the list.</summary>
public sealed class PageItem : ObservableObject
{
    string m_Filename = "";
    bool m_IsChecked;

    /// <summary>The filename.</summary>
    public string Filename
    {
        get => m_Filename;
        set => SetProperty(ref m_Filename, value);
    }

    /// <summary>Whether it is checked as the target of the operation.</summary>
    public bool IsChecked
    {
        get => m_IsChecked;
        set => SetProperty(ref m_IsChecked, value);
    }
}
