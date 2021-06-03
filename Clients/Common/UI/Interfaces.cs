using System.Collections.ObjectModel;

namespace Viking.Common
{
    /// <summary>
    /// Provides an array of strings used to provide help in context for users
    /// </summary>
    public interface IHelpStrings
    {
        string[] HelpStrings { get; }
    }

    public interface IObservableHelpStrings
    {   
        ObservableCollection<string> ObservableHelpStrings { get; }
    }
}
