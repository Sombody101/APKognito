namespace APKognito.ApkMod.Automation;

public static class PathFilterExtensions
{
    public static IEnumerable<string> FilterByCommandResult(
        this IEnumerable<string> paths,
        CommandStageResult? filter
    )
    {
        if (filter is null)
        {
            return paths;
        }

        IEnumerable<string> filteredPaths = paths.Where(path => !filter.Exclusions.Exists(exclude => path.Contains(exclude)));

        if (filter.Inclusions is not null && filter.Inclusions.Count != 0)
        {
            filteredPaths = filteredPaths.Where(path => filter.Inclusions.Exists(include => path.Contains(include)));
        }

        return filteredPaths;
    }
}