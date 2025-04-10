namespace APKognito.Models;

public class ComboItemPair<T>(string displayName, T value)
{
    public string DisplayName { get; set; } = displayName;
    public T Value { get; set; } = value;

    public override string ToString()
    {
        return DisplayName;
    }
}
