namespace APKognito.ApkLib.Interfaces;

public interface IIncludable
{
    public IEnumerable<string> DefinedInclusions { get; }

    public IEnumerable<string> DefinedExclusions { get; }
}
