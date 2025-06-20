using Microsoft.Extensions.Logging;

namespace APKognito.Legacy.ApkLib.Automation.CommandBindings;

internal static partial class CommandBindings
{
    public static bool LikelyDirectory(string path)
    {
        return path.EndsWith('/') || path.EndsWith('\\');
    }

    public static class FileCommands
    {
        [Command("mkdir", 1, FileAccess.Write)]
        public static void CreateDirectory(string target, ILogger logger)
        {
            logger.LogInformation("Creating directory: {Target}", target);
            Directory.CreateDirectory(target);
        }

        [Command("mv", 2, FileAccess.Read, FileAccess.Write)]
        public static void MoveEntry(string source, string target, ILogger logger)
        {
            logger.LogInformation("Moving: {{\n\tSource: {Source}\n\tTarget: {Target}\n}}", source, target);

            if (!LikelyDirectory(source) || File.Exists(source))
            {
                File.Move(source, target, true);
            }
            else if (Directory.Exists(source))
            {
                Directory.Move(source, target);
            }
            else
            {
                throw new FileNotFoundException($"Source '{source}' not found.");
            }
        }

        [Command("cp", 2, FileAccess.Read, FileAccess.Write)]
        public static void CopyEntry(string source, string target, ILogger logger)
        {
            logger.LogInformation("Copying: {{\n\tSource: {Source}\n\tTarget: {Target}\n}}", source, target);

            if (!LikelyDirectory(source) || File.Exists(source))
            {
                File.Copy(source, target);
            }
            else if (Directory.Exists(source))
            {
                CopyDirectory(source, target);
            }
            else
            {
                throw new FileNotFoundException($"Source '{source}' not found.");
            }
        }

        [Command("rm", CommandAttribute.ANY, FileAccess.Write)]
        public static void RemoveEntry(string[] args, ILogger logger)
        {
            if (args is null || args.Length is 0)
            {
                throw new ArgumentException("No targets specified.");
            }

            foreach (string target in args)
            {
                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                if (!LikelyDirectory(target) || File.Exists(target))
                {
                    logger.LogInformation("Removing file: {Target}", target);
                    File.Delete(target);
                }
                else if (Directory.Exists(target))
                {
                    logger.LogInformation("Removing directory '{Target}' (recursively)", target);
                    Directory.Delete(target, true);
                }
                else
                {
                    throw new FileNotFoundException($"Target '{target}' not found.");
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            DirectoryInfo dir = new(sourceDir);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            _ = Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                _ = file.CopyTo(targetFilePath);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}
