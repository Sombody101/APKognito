namespace APKognito.ApkLib.Utilities;

internal static class EnumerableFilter
{
    public static IEnumerable<string> FilterByAdditions(
        this IEnumerable<string> paths,
        IEnumerable<string> inclusions,
        IEnumerable<string> exclusions
    )
    {
        IEnumerable<string> filteredPaths = paths;

        if (exclusions.Any())
        {
            filteredPaths = filteredPaths.Where(path => !exclusions.Any(exclude => path.Contains(exclude)));
        }

        if (inclusions.Any())
        {
            filteredPaths = filteredPaths.Where(path => inclusions.Any(include => path.Contains(include)));
        }

        return filteredPaths;
    }
}
