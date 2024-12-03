using Humanizer;

namespace APKognito.Models;

public class HumanComboBoxItem<T> where T : Enum
{
    public string DisplayName { get; set; }
    public T Value { get; set; }

    public HumanComboBoxItem(T value)
    {
        Value = value;
        DisplayName = value.Humanize(LetterCasing.Title);
    }

    public override string ToString()
    {
        return DisplayName;
    }
}
