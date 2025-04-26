
using System.Windows.Markup;

namespace APKognito.Helpers;

public class EnumCollectionExtension : MarkupExtension
{
    public Type EnumType { get; set; } = null!;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (EnumType is not null)
        {
            return CreateEnumValueList(EnumType);
        }

        return default!;
    }

    private static List<object> CreateEnumValueList(Type enumType)
    {
        return Enum.GetNames(enumType)
            .Select(name => Enum.Parse(enumType, name))
            .ToList();
    }
}