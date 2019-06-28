using FS.Sync;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static GuiLabs.Common.Utilities;

namespace GuiLabs.FileUtilities
{
    public static class Sync
    {
        /// <summary>
        /// Assumes source directory exists. destination may or may not exist.
        /// </summary>
        public static async Task Directories(string source, string destination, Arguments arguments, Log log, CancellationToken token)
        {
            if (!Directory.Exists(destination))
            {
                FileSystem.CreateDirectory(destination, arguments.WhatIf, log);
            }

            source = Paths.TrimSeparator(source);
            destination = Paths.TrimSeparator(destination);

            var diff = await Folders.DiffFolders(
                source,
                destination,
                arguments.Pattern,
                log,
                token,
                recursive: !arguments.Nonrecursive,
                compareContents:
                    arguments.UpdateChangedFiles
                    || arguments.DeleteChangedFiles
                    || arguments.DeleteSameFiles,
                arguments.RespectLastAccessDateTime).ConfigureAwait(false);

            bool changesMade = false;
            int filesFailedToCopy = 0;
            int filesFailedToDelete = 0;
            int foldersFailedToCreate = 0;
            int foldersFailedToDelete = 0;

            if (arguments.CopyLeftOnlyFiles)
            {
                using (log.MeasureTime("Copying new files"))
                {
                    foreach (var leftOnly in diff.LeftOnlyFiles)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        var destinationFilePath = destination + leftOnly;
                        if (!FileSystem.CopyFile(source + leftOnly, destinationFilePath, arguments.WhatIf, log))
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
                        if (!FileSystem.CopyFile(source + changed, destinationFilePath, arguments.WhatIf, log))
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
                        if (!FileSystem.DeleteFile(destinationFilePath, arguments.WhatIf, log))
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
                        if (!FileSystem.DeleteFile(destinationFilePath, arguments.WhatIf, log))
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
                        if (!FileSystem.DeleteFile(deletedFilePath, arguments.WhatIf, log))
                        {
                            filesFailedToDelete++;
                        }

                        changesMade = true;
                    }
                }
            }

            int foldersCreated = 0;
            if (arguments.CopyEmptyDirectories)
            {
                using (log.MeasureTime("Creating folders"))
                {
                    foreach (var leftOnlyFolder in diff.LeftOnlyFolders)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        var newFolder = destination + leftOnlyFolder;
                        if (!Directory.Exists(newFolder))
                        {
                            if (!FileSystem.CreateDirectory(newFolder, arguments.WhatIf, log))
                            {
                                foldersFailedToCreate++;
                            }
                            else
                            {
                                foldersCreated++;
                            }

                            changesMade = true;
                        }
                    }
                }
            }

            int foldersDeleted = 0;
            if (arguments.DeleteRightOnlyDirectories)
            {
                using (log.MeasureTime("Deleting folders"))
                {
                    foreach (var rightOnlyFolder in diff.RightOnlyFolders)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        var deletedFolderPath = destination + rightOnlyFolder;
                        if (Directory.Exists(deletedFolderPath))
                        {
                            if (!FileSystem.DeleteDirectory(deletedFolderPath, arguments.WhatIf, log))
                            {
                                foldersFailedToDelete++;
                            }
                            else
                            {
                                foldersDeleted++;
                            }

                            changesMade = true;
                        }
                    }
                }
            }

            if (diff.LeftOnlyFiles.Any() && arguments.CopyLeftOnlyFiles)
            {
                var count = diff.LeftOnlyFiles.Count();
                var fileOrFiles = Pluralize("file", count);
                if (arguments.WhatIf)
                {
                    log.WriteLine($"Would have copied {count} new {fileOrFiles}", ConsoleColor.Green);
                }
                else
                {
                    log.WriteLine($"{count} new {fileOrFiles} copied", ConsoleColor.Green);
                }
            }

            if (foldersCreated > 0 && arguments.CopyEmptyDirectories)
            {
                var folderOrFolders = Pluralize("folder", foldersCreated);
                if (arguments.WhatIf)
                {
                    log.WriteLine($"Would have created {foldersCreated} {folderOrFolders}", ConsoleColor.Green);
                }
                else
                {
                    log.WriteLine($"{foldersCreated} {folderOrFolders} created", ConsoleColor.Green);
                }
            }

            if (diff.ChangedFiles.Any() && arguments.UpdateChangedFiles)
            {
                var count = diff.ChangedFiles.Count();
                var fileOrFiles = Pluralize("file", count);
                if (arguments.WhatIf)
                {
                    log.WriteLine($"Would have updated {count} changed {fileOrFiles}", ConsoleColor.Yellow);
                }
                else
                {
                    log.WriteLine($"{count} changed {fileOrFiles} updated", ConsoleColor.Yellow);
                }
            }

            if (diff.ChangedFiles.Any() && arguments.DeleteChangedFiles)
            {
                var count = diff.ChangedFiles.Count();
                var fileOrFiles = Pluralize("file", count);
                if (arguments.WhatIf)
                {
                    log.WriteLine($"Would have deleted {count} changed {fileOrFiles}", ConsoleColor.Yellow);
                }
                else
                {
                    log.WriteLine($"{count} changed {fileOrFiles} deleted", ConsoleColor.Yellow);
                }
            }

            if (diff.RightOnlyFiles.Any() && arguments.DeleteRightOnlyFiles)
            {
                var count = diff.RightOnlyFiles.Count();
                var fileOrFiles = Pluralize("file", count);
                if (arguments.WhatIf)
                {
                    log.WriteLine($"Would have deleted {count} right-only {fileOrFiles}", ConsoleColor.Red);
                }
                else
                {
                    log.WriteLine($"{count} right-only {fileOrFiles} deleted", ConsoleColor.Red);
                }
            }

            if (foldersDeleted > 0 && arguments.DeleteRightOnlyDirectories)
            {
                var folderOrFolders = Pluralize("folder", foldersDeleted);
                if (arguments.WhatIf)
                {
                    log.WriteLine($"Would have deleted {foldersDeleted} right-only {folderOrFolders}", ConsoleColor.Red);
                }
                else
                {
                    log.WriteLine($"{foldersDeleted} right-only {folderOrFolders} deleted", ConsoleColor.Red);
                }
            }

            if (diff.IdenticalFiles.Any())
            {
                var count = diff.IdenticalFiles.Count();
                var fileOrFiles = Pluralize("file", count);
                if (arguments.DeleteSameFiles)
                {
                    if (arguments.WhatIf)
                    {
                        log.WriteLine($"Would have deleted {count} identical {fileOrFiles} from destination", ConsoleColor.White);
                    }
                    else
                    {
                        log.WriteLine($"{count} identical {fileOrFiles} deleted from destination", ConsoleColor.White);
                    }
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

            if (foldersFailedToCreate > 0)
            {
                log.WriteLine($"Failed to create {foldersFailedToCreate} {Pluralize("folder", foldersFailedToCreate)}.", ConsoleColor.Red);
            }

            if (foldersFailedToDelete > 0)
            {
                log.WriteLine($"Failed to delete {foldersFailedToDelete} {Pluralize("folder", foldersFailedToDelete)}.", ConsoleColor.Red);
            }

            if (!changesMade)
            {
                if (arguments.WhatIf)
                {
                    log.WriteLine("Would have made no changes.", ConsoleColor.White);
                }
                else
                {
                    log.WriteLine("Made no changes.", ConsoleColor.White);
                }
            }

            // if there were no errors, delete the cache of the folder contents. Otherwise
            // chances are they're going to restart the process, so we might needs the cache
            // next time.
            if (filesFailedToCopy == 0
                && filesFailedToDelete == 0
                && foldersFailedToCreate == 0
                && foldersFailedToDelete == 0)
            {
                DirectoryContentsCache.ClearWrittenFilesFromCache();
            }

            log.PrintFinalReport();
        }

        /// <summary>
        /// Assumes source exists, destination may or may not exist.
        /// If it exists and is identical bytes to source, nothing is done.
        /// If it exists and is different, it is overwritten.
        /// If it doesn't exist, source is copied.
        /// </summary>
        public static void Files(string source, string destination, Arguments arguments, Log log, CancellationToken token)
        {
            if (File.Exists(destination) && FileUtilities.Files.AreContentsIdentical(source, destination, token))
            {
                log.WriteLine("File contents are identical.", ConsoleColor.White);
                return;
            }

            FileSystem.CopyFile(source, destination, arguments.WhatIf, log);
        }
    }
}
