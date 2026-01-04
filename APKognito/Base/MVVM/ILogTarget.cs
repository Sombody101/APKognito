namespace APKognito.Base.MVVM;

using ElementCollection = System.Collections.ObjectModel.ObservableCollection<object>;

public interface ILogTarget
{
    ElementCollection LogEnties { get; }
}
