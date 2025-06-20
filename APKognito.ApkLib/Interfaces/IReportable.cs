namespace APKognito.ApkLib.Interfaces;

public interface IReportable<TImplementor> where TImplementor : IReportable<TImplementor>
{
    /// <summary>
    /// Sets the <see cref="IProgress{ProgressInfo}"/> instance associated with the <typeparamref name="TImplementor"/>
    /// </summary>
    /// <param name="reporter"></param>
    /// <returns></returns>
    public TImplementor SetReporter(IProgress<ProgressInfo>? reporter);
}
