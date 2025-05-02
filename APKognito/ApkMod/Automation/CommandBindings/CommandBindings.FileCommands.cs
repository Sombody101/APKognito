using APKognito.Utilities.MVVM;
using System.IO;

namespace APKognito.ApkMod.Automation.CommandBindings;

internal static partial class CommandBindings
{
    public static bool LikelyDirectory(string path)
    {
        return path.EndsWith('/') || path.EndsWith('\\');
    }

    public static class FileCommands
    {
        [Command("mkdir", 1, FileAccess.Write)]
        public static void CreateDirectory(string target, IViewLogger logger)
        {
            logger.Log($"Creating directory: {target}");
            Directory.CreateDirectory(target);
        }

        [Command("mv", 2, FileAccess.Read, FileAccess.Write)]
        public static void MoveEntry(string source, string target, IViewLogger logger)
        {
            logger.Log($"Moving: {{\n\tSource: {source}\n\tTarget: {target}\n}}");

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
        public static void CopyEntry(string source, string target, IViewLogger logger)
        {
            logger.Log($"Copying: {{\n\tSource: {source}\n\tTarget: {target}\n}}");

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
        public static void RemoveEntry(string[] args, IViewLogger logger)
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
                    logger.Log($"Removing file: {target}");
                    File.Delete(target);
                }
                else if (Directory.Exists(target))
                {
                    logger.Log($"Removing directory '{target}' (recursively)");
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
