using APKognito.ApkLib.Automation;

namespace APKognito.ApkLib.Interfaces;

public abstract class Additionals<TImplementor> where TImplementor : Additionals<TImplementor>
{
    protected IEnumerable<string> Inclusions = [];

    protected IEnumerable<string> Exclusions = [];

    public virtual TImplementor WithAdditions(IEnumerable<string> inclusions, IEnumerable<string> exclusions)
    {
        Inclusions = Inclusions.Union(inclusions).ToList();
        Exclusions = Exclusions.Union(exclusions).ToList();
        return (TImplementor)this;
    }

    public virtual TImplementor WithStageResult(CommandStageResult? stage)
    {
        if (stage is null)
        {
            return (TImplementor)this;
        }

        Inclusions = Inclusions.Union(stage.Inclusions).ToList();
        Exclusions = Exclusions.Union(stage.Exclusions).ToList();
        return (TImplementor)this;
    }

    public virtual TImplementor WithInclusions(IEnumerable<string> inclusions)
    {
        Inclusions = Inclusions.Union(inclusions).ToList();
        return (TImplementor)this;
    }

    public virtual TImplementor WithExclusions(IEnumerable<string> exclusions)
    {
        Exclusions = Exclusions.Union(exclusions).ToList();
        return (TImplementor)this;
    }
}
