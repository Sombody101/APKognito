using APKognito.ApkLib.Configuration;

namespace APKognito.ApkLib.Interfaces;

public interface IPackageEditor
{
    Task ExecuteAsync(PackageRenameState state, BaseRenameConfiguration config, CancellationToken token = default);
}
