using System;
using System.ComponentModel;
using System.Reflection;
using System.Security.Cryptography.Pkcs;
using System.Security.Permissions;
using System.Windows.Media.Animation;

namespace APKognito.Cli;

internal class ParsedArgs
{
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
                prop => (CliArgAttribute)prop.GetCustomAttribute<CliArgAttribute>()!
            );

        List<string> errorSwitches = [];

        for (int i = 0; i < args.Length; ++i)
        {
            string arg = args[i];

            if (arg == "--")
            {
                // Ignore everything after
                return;
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
                errorSwitches.Add($"Unexpected value '{arg}' at arg index {i}");
                continue;
            }

            if (wantedArg.Key is null)
            {
                errorSwitches.Add($"Unknown switch '{fixedArg}'");
                continue;
            }

            // Check if switch has input value
            if (wantedArg.Value.SwitchValueType is not null)
            {
                if (i + 1 >= args.Length)
                {
                    errorSwitches.Add($"No value given for switch '{arg}'");
                    break;
                }

                string? error = TrySetPropertyValue(wantedArg.Key, wantedArg.Value.SwitchValueType, args[++i]);

                if (error is not null)
                {
                    errorSwitches.Add($"Invalid value '{arg}' for switch {wantedArg.Value.Switch ?? wantedArg.Value.ShorthandSwitch.ToString()}: {error}");
                }
            }
            else
            {
                wantedArg.Key.SetValue(this, true);
            }
        }

        if (errorSwitches.Count > 0)
        {
            foreach (string error in errorSwitches)
            {
                Console.WriteLine(error);
            }

            CliMain.Exit(ExitCode.InvalidCliUsage);
        }
    }

    private string? TrySetPropertyValue(PropertyInfo property, Type type, string value)
    {
        try
        {
            object? propValue = TypeDescriptor.GetConverter(type).ConvertFromString(value);
            property.SetValue(this, propValue);

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
