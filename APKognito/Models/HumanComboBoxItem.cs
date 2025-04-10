using Humanizer;

namespace APKognito.Models;

public class HumanComboBoxItem<T>(T value) where T : Enum
{
    public string DisplayName { get; set; } = value.Humanize(LetterCasing.Title);
    public T Value { get; set; } = value;

    public override string ToString()
    {
        return DisplayName;
    }
}
