using System.Diagnostics;
using System.Runtime.InteropServices;

namespace APKognito.Cli;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
internal class CliArgAttribute : Attribute
{
    public char? ShorthandSwitch { get; }

    public string? Switch { get; }

    public Type? SwitchValueType { get; }

    public CliArgAttribute(string arg, [Optional] Type? valueType)
    {
        string[] split = arg.Split('|');

        if (split.Length is 2)
        {
            ShorthandSwitch = split[0][0];
            Switch = split[1];
        }
        else
        {
            if (split[0].Length == 1)
            {
                ShorthandSwitch = split[0][0];
                Switch = null;
            }
            else
            {
                ShorthandSwitch = null;
                Switch = split[0];
            }
        }

        if (valueType is not null)
        {
            SwitchValueType = valueType;
        }
    }
}
