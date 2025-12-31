using System.Runtime.Serialization;
using Tomlet.Attributes;

namespace APKognito.Configurations.ConfigModels;

/*
 * This model is for the "anchor" file that helps with portability. It lives here to be cleaner, but is not used or associated with ConfigurationFactory or other models.
 */

internal sealed record ApplicationAnchor
{
    [DataMember(Name = "DataRoot")]
    [TomlProperty("DataRoot")]
    public string? OverrideBasePath
    {
        get => field;
        set
        {
            if (value?.Length is 0)
            {
                throw new InvalidAnchorDataException("Override data root cannot be empty. Did you mean the current directory? (e.g., '.')");
            }

            field = value;
        }
    }

    public class InvalidAnchorDataException(string message) : Exception(message);
}
