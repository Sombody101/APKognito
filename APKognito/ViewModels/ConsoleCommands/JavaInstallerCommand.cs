using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using APKognito.AdbTools;
using APKognito.Utilities;
using APKognito.Utilities.JavaTools;
using APKognito.Utilities.MVVM;

namespace APKognito.ViewModels.ConsoleCommands;

internal static class JavaInstallerCommand
{
    private const string ORACLE_JAVA_RELEASES_URL = "https://java.oraclecloud.com/currentJavaReleases/";

    public static async Task InstallJavaCommandAsync(IViewLogger logger, CancellationToken token)
    {
        logger.Log("Installing JDK 24...");

        string tempDirectory = Path.Combine(Path.GetTempPath(), "APKognito-JavaTmp");
        _ = Directory.CreateDirectory(tempDirectory);
        _ = DirectoryManager.ClaimDirectory(tempDirectory);

        string javaDownload = Path.Combine(tempDirectory, "jdk-24.exe");

        if (!File.Exists(javaDownload))
        {
            using (IDisposable? scope = logger.BeginScope("CLIENT"))
            {
                bool result = await WebGet.DownloadAsync(AdbManager.JDK_24_INSTALL_EXE_LINK, javaDownload, logger, token);

                if (!result)
                {
                    logger.LogError("Failed to install JDK 24.");
                    return;
                }
            }
        }
        else
        {
            logger.Log("Using previously downloaded installer.");
        }

        using Process installer = new()
        {
            StartInfo = new()
            {
                FileName = javaDownload,
                UseShellExecute = true,
                Verb = "runas",
            }
        };

        try
        {
            logger.Log("Waiting for installer to exit...");
            _ = installer.Start();
            await installer.WaitForExitAsync(token);

            // 0: Install successful
            // 1602: Any kind of user decline during the installer (including if it was already installed and user was prompted for removal), or, if the installer feels like fucking with you.
            if (installer.ExitCode is not 0)
            {
                logger.LogWarning("Java install likely aborted! Checking Java executable path...");
            }
            else
            {
                logger.LogSuccess("JDK 24 installed successfully! Checking Java executable path...");
            }

            JavaVersionInformation? foundVersion = JavaVersionCollector.RefreshJavaVersions().FirstOrDefault(v => v.Version.Major is 24);

            if (foundVersion is not null)
            {
                logger.LogSuccess($"Detected {foundVersion}");
            }
            else
            {
                logger.LogError("Failed to detect newly installed JDK. Try restarting APKognito, your computer, or reinstalling.");
                return;
            }

            File.Delete(javaDownload);
        }
        catch (Win32Exception)
        {
            logger.LogWarning("Installer canceled.");
            return;
        }
        finally
        {
            if (File.Exists(javaDownload))
            {
                logger.Log($"The JDK installer has not been deleted in case you want to install later. You can find it in the Drive Footprint page, or:\n{javaDownload}");
            }
        }
    }
}
