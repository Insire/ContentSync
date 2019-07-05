using FS.Sync;
using MvvmScarletToolkit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace GuiLabs.FileUtilities
{
    public static class Folders
    {
        /// <summary>
        /// Assumes leftRoot is an existing folder. rightRoot may not exist if operating in speculative mode.
        /// </summary>
        public static FolderDiffResults DiffFolders(
            string leftRoot,
            string rightRoot,
            Log log,
            CancellationToken token,
            bool compareContents = true,
            bool respectDate = true)
        {
            var leftRelativePaths = new ConcurrentBag<string>();

            using (log.MeasureTime("Scanning source directory"))
            {
                GetRelativePathsOfAllFiles(leftRoot, leftRelativePaths, token);
            }

            var rightRelativePaths = new ConcurrentBag<string>();

            if (Directory.Exists(rightRoot))
            {
                using (log.MeasureTime("Scanning destination directory"))
                {
                    GetRelativePathsOfAllFiles(rightRoot, rightRelativePaths, token);
                }
            }

            var leftOnlyFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var identicalFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rightOnlyFiles = new HashSet<string>(rightRelativePaths, StringComparer.OrdinalIgnoreCase);

            using (log.MeasureTime("Comparing"))
            {
                leftRelativePaths
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ForEach(AnalyzeFile);
            }

            using (log.MeasureTime("Sorting"))
            {
                var leftOnlyFilesList = leftOnlyFiles.ToList();
                leftOnlyFilesList.Sort();

                var identicalFilesList = identicalFiles.ToList();
                identicalFilesList.Sort();

                var changedFilesList = changedFiles.ToList();
                changedFilesList.Sort();

                return new FolderDiffResults(
                    leftOnlyFilesList,
                    identicalFilesList,
                    changedFilesList,
                    rightOnlyFiles.OrderBy(s => s).ToArray());
            }

            void AnalyzeFile(string path)
            {
                var leftFullPath = leftRoot + path;
                var rightFullPath = rightRoot + path;

                var rightContains = rightRelativePaths.Contains(path);
                if (rightContains)
                {
                    var areSame = true;
                    try
                    {
                        areSame = !compareContents
                        || Files.AreContentsIdentical(leftFullPath, rightFullPath, token)
                        || respectDate
                            ? File.GetLastWriteTimeUtc(leftFullPath) <= File.GetLastWriteTimeUtc(rightFullPath)
                            : true;
                    }
                    catch (Exception ex)
                    {
                        log.WriteError(ex.ToString());
                        return;
                    }

                    if (areSame)
                    {
                        identicalFiles.Add(path);
                    }
                    else
                    {
                        changedFiles.Add(path);
                    }
                }
                else
                {
                    leftOnlyFiles.Add(path);
                }

                rightOnlyFiles.Remove(path);
            }
        }

        private static readonly FieldInfo pathField = typeof(FileSystemInfo).GetField("FullPath", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void GetRelativePathsOfAllFiles(string rootFolder, ConcurrentBag<string> files, CancellationToken token)
        {
            var rootDirectoryInfo = new DirectoryInfo(rootFolder);
            var prefixLength = rootFolder.Length;

            Parallel.ForEach(rootDirectoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly), fileSystemInfo =>
            {
                if (token.IsCancellationRequested)
                    return;

                string relativePath = (string)pathField.GetValue(fileSystemInfo);
                relativePath = relativePath.Substring(prefixLength);

                files.Add(relativePath);
            });
        }
    }
}
