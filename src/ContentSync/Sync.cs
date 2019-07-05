using FS.Sync;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using static GuiLabs.Common.Utilities;

namespace GuiLabs.FileUtilities
{
    public static class Sync
    {
        /// <summary>
        /// Assumes source directory exists. destination may or may not exist.
        /// </summary>
        public static void Directories(string source, string destination, Log log, CancellationToken token, Arguments arguments)
        {
            if (!Directory.Exists(destination))
            {
                FileSystem.CreateDirectory(destination, log);
            }

            source = Paths.TrimSeparator(source);
            destination = Paths.TrimSeparator(destination);

            var diff = Folders.DiffFolders(
                source,
                destination,
                log,
                token,
                compareContents:
                    arguments.UpdateChangedFiles
                    || arguments.DeleteChangedFiles
                    || arguments.DeleteSameFiles,
                arguments.RespectLastAccessDateTime);

            bool changesMade = false;
            int filesFailedToCopy = 0;
            int filesFailedToDelete = 0;

            if (arguments.CopyLeftOnlyFiles)
            {
                using (log.MeasureTime("Copying new files"))
                {
                    foreach (var leftOnly in diff.LeftOnlyFiles)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        var destinationFilePath = destination + leftOnly;
                        if (!FileSystem.CopyFile(source + leftOnly, destinationFilePath, log))
                        {
                            filesFailedToCopy++;
                        }

                        changesMade = true;
                    }
                }
            }

            if (arguments.UpdateChangedFiles)
            {
                using (log.MeasureTime("Updating changed files"))
                {
                    foreach (var changed in diff.ChangedFiles)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        var destinationFilePath = destination + changed;
                        if (!FileSystem.CopyFile(source + changed, destinationFilePath, log))
                        {
                            filesFailedToCopy++;
                        }

                        changesMade = true;
                    }
                }
            }
            else if (arguments.DeleteChangedFiles)
            {
                using (log.MeasureTime("Deleting changed files"))
                {
                    foreach (var changed in diff.ChangedFiles)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        var destinationFilePath = destination + changed;
                        if (!FileSystem.DeleteFile(destinationFilePath, log))
                        {
                            filesFailedToDelete++;
                        }

                        changesMade = true;
                    }
                }
            }

            if (arguments.DeleteSameFiles)
            {
                using (log.MeasureTime("Deleting identical files"))
                {
                    foreach (var same in diff.IdenticalFiles)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        var destinationFilePath = destination + same;
                        if (!FileSystem.DeleteFile(destinationFilePath, log))
                        {
                            filesFailedToDelete++;
                        }

                        changesMade = true;
                    }
                }
            }

            if (arguments.DeleteRightOnlyFiles)
            {
                using (log.MeasureTime("Deleting extra files"))
                {
                    foreach (var rightOnly in diff.RightOnlyFiles)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        var deletedFilePath = destination + rightOnly;
                        if (!FileSystem.DeleteFile(deletedFilePath, log))
                        {
                            filesFailedToDelete++;
                        }

                        changesMade = true;
                    }
                }
            }

            if (diff.LeftOnlyFiles.Any() && arguments.CopyLeftOnlyFiles)
            {
                var count = diff.LeftOnlyFiles.Count();
                var fileOrFiles = Pluralize("file", count);

                log.WriteLine($"{count} new {fileOrFiles} copied", ConsoleColor.Green);
            }

            if (diff.ChangedFiles.Any() && arguments.UpdateChangedFiles)
            {
                var count = diff.ChangedFiles.Count();
                var fileOrFiles = Pluralize("file", count);

                log.WriteLine($"{count} changed {fileOrFiles} updated", ConsoleColor.Yellow);
            }

            if (diff.ChangedFiles.Any() && arguments.DeleteChangedFiles)
            {
                var count = diff.ChangedFiles.Count();
                var fileOrFiles = Pluralize("file", count);

                log.WriteLine($"{count} changed {fileOrFiles} deleted", ConsoleColor.Yellow);
            }

            if (diff.RightOnlyFiles.Any() && arguments.DeleteRightOnlyFiles)
            {
                var count = diff.RightOnlyFiles.Count();
                var fileOrFiles = Pluralize("file", count);

                log.WriteLine($"{count} right-only {fileOrFiles} deleted", ConsoleColor.Red);
            }

            if (diff.IdenticalFiles.Any())
            {
                var count = diff.IdenticalFiles.Count();
                var fileOrFiles = Pluralize("file", count);
                if (arguments.DeleteSameFiles)
                {
                    log.WriteLine($"{count} identical {fileOrFiles} deleted from destination", ConsoleColor.White);
                }
                else
                {
                    log.WriteLine($"{count} identical {fileOrFiles}", ConsoleColor.White);
                }
            }

            if (filesFailedToCopy > 0)
            {
                log.WriteLine($"Failed to copy {filesFailedToCopy} {Pluralize("file", filesFailedToCopy)}", ConsoleColor.Red);
            }

            if (filesFailedToDelete > 0)
            {
                log.WriteLine($"Failed to delete {filesFailedToDelete} {Pluralize("file", filesFailedToDelete)}.", ConsoleColor.Red);
            }

            if (!changesMade)
            {
                log.WriteLine("Made no changes.", ConsoleColor.White);
            }

            log.PrintFinalReport();
        }
    }
}
