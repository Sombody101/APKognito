namespace APKognito.ApkLib.Interfaces;

internal interface IBuildable<out TOut>
{
    public TOut Build();
}
