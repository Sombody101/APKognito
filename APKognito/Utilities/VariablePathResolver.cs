using System.Text.RegularExpressions;

namespace APKognito.Utilities;

public static partial class VariablePathResolver
{
    public static string Resolve(string path)
    {
        Match match = GetPathVariable().Match(path);

        if (!match.Success)
        {
            return path;
        }

        string varName = match.Groups["var_name"].Value;
        string? envVar = Environment.GetEnvironmentVariable(varName);

        if (envVar is not null)
        {
            path = path.Replace(match.Value, envVar);
        }

        return path;
    }

    [GeneratedRegex(@"\%(?<var_name>[a-zA-Z_-]+)\%")]
    private static partial Regex GetPathVariable();
}
