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
        public static async Task<FolderDiffResults> DiffFolders(
            string leftRoot,
            string rightRoot,
            string pattern,
            Log log,
            CancellationToken token,
            bool recursive = true,
            bool compareContents = true,
            bool respectDate = true)
        {
            var leftRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var leftOnlyFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (log.MeasureTime("Scanning source directory"))
            {
                GetRelativePathsOfAllFiles(leftRoot, pattern, recursive, leftRelativePaths, leftOnlyFolders);
            }

            var rightRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rightOnlyFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(rightRoot))
            {
                using (log.MeasureTime("Scanning destination directory"))
                {
                    GetRelativePathsOfAllFiles(rightRoot, pattern, recursive, rightRelativePaths, rightOnlyFolders);
                }
            }

            var leftOnlyFiles = new ConcurrentBag<string>();
            var identicalFiles = new ConcurrentBag<string>();
            var changedFiles = new ConcurrentBag<string>();
            var rightOnlyFiles = new ConcurrentDictionary<string, string>(rightRelativePaths.Select(p => new KeyValuePair<string, string>(p, p)), StringComparer.OrdinalIgnoreCase);

            var commonFolders = leftOnlyFolders.Intersect(rightOnlyFolders, StringComparer.OrdinalIgnoreCase).ToArray();
            leftOnlyFolders.ExceptWith(commonFolders);
            rightOnlyFolders.ExceptWith(commonFolders);

            using (log.MeasureTime("Comparing"))
            {
                await leftRelativePaths
                    .ForEachAsync(left => Task.Run(() => AnalyzeFile(left), token), Environment.ProcessorCount * 2)
                    .ConfigureAwait(false);
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
                    rightOnlyFiles.Select(p => p.Key).OrderBy(s => s).ToArray(),
                    leftOnlyFolders.OrderBy(s => s).ToArray(),
                    rightOnlyFolders.OrderBy(s => s).ToArray());
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
                        || Files.AreContentsIdentical(leftFullPath, rightFullPath)
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

                rightOnlyFiles.TryRemove(path, out _);
            }
        }

        private static readonly FieldInfo pathField = typeof(FileSystemInfo).GetField("FullPath", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void GetRelativePathsOfAllFiles(string rootFolder, string pattern, bool recursive, HashSet<string> files, HashSet<string> folders)
        {
            // don't go through the cache for non-recursive case
            if (recursive && DirectoryContentsCache.TryReadFromCache(rootFolder, pattern, files, folders))
            {
                return;
            }

            var rootDirectoryInfo = new DirectoryInfo(rootFolder);
            var prefixLength = rootFolder.Length;
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var fileSystemInfo in rootDirectoryInfo.EnumerateFileSystemInfos(pattern, searchOption))
            {
                string relativePath = (string)pathField.GetValue(fileSystemInfo);
                relativePath = relativePath.Substring(prefixLength);
                if (fileSystemInfo is FileInfo)
                {
                    files.Add(relativePath);
                }
                else if (recursive)
                {
                    folders.Add(relativePath);
                }
            }

            if (recursive)
            {
                DirectoryContentsCache.SaveToCache(rootFolder, pattern, files, folders);
            }
        }
    }
}
