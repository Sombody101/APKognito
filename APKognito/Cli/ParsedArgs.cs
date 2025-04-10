using System.ComponentModel;
using System.Reflection;

namespace APKognito.Cli;

internal class ParsedArgs
{
    private readonly List<string> ErroredSwitches = [];

    public bool RunningCli { get; }

    [CliArg("getcode", typeof(int))]
    public int? GetCode { get; set; }

    [CliArg("v|version")]
    public bool GetVersion { get; set; }

    [CliArg("start")]
    public bool StartApp { get; set; }

    public ParsedArgs(string[] args)
    {
        if (args.Length == 0)
        {
            RunningCli = false;
            return;
        }

        RunningCli = true;
        StartArgParse(args);
    }

    private void StartArgParse(string[] args)
    {
        Dictionary<PropertyInfo, CliArgAttribute> argTypes = typeof(ParsedArgs).GetProperties()
            .Where(field => Attribute.IsDefined(field, typeof(CliArgAttribute)))
            .ToDictionary(
                prop => prop,
                prop => prop.GetCustomAttribute<CliArgAttribute>()!
            );

        for (int i = 0; i < args.Length; ++i)
        {
            string arg = args[i];

            if (arg == "--")
            {
                // Ignore everything after
                break;
            }

            KeyValuePair<PropertyInfo, CliArgAttribute> wantedArg;
            string fixedArg;

            if (arg.StartsWith("--"))
            {
                fixedArg = arg[2..];
                wantedArg = argTypes.FirstOrDefault(argT => argT.Value.Switch == fixedArg);
            }
            else if (arg.StartsWith('-'))
            {
                char cfixedArg = arg[1];
                wantedArg = argTypes.FirstOrDefault(argT => argT.Value.ShorthandSwitch == cfixedArg);

                fixedArg = cfixedArg.ToString();
            }
            else
            {
                ErroredSwitches.Add($"Unexpected value '{arg}' at arg index {i}");
                continue;
            }

            if (wantedArg.Key is null)
            {
                ErroredSwitches.Add($"Unknown switch '{fixedArg}'");
                continue;
            }

            ApplyPropertyValue(wantedArg.Key, wantedArg.Value, args, ref i);
        }

        if (ErroredSwitches.Count > 0)
        {
            foreach (string error in ErroredSwitches)
            {
                Console.WriteLine(error);
            }

            CliMain.Exit(ExitCode.InvalidCliUsage);
        }
    }

    private void ApplyPropertyValue(PropertyInfo property, CliArgAttribute attribute, string[] args, ref int index)
    {
        string arg = args[index];

        // Check if switch has input value
        if (attribute.SwitchValueType is not null)
        {
            if (index + 1 >= args.Length)
            {
                ErroredSwitches.Add($"No value given for switch '{arg}'");
                return;
            }

            try
            {
                object? propValue = TypeDescriptor.GetConverter(attribute.SwitchValueType).ConvertFromString(args[++index]);
                property.SetValue(this, propValue);
            }
            catch (Exception ex)
            {
                ErroredSwitches.Add($"Invalid value '{arg}' for switch {attribute.Switch ?? attribute.ShorthandSwitch.ToString()}: {ex.Message}");
            }
        }
        else
        {
            property.SetValue(this, true);
        }
    }
}