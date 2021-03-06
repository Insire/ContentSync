﻿using FS.Sync;
using System;
using System.IO;

namespace GuiLabs.FileUtilities
{
    public static class FileSystem
    {
        public static bool CopyFile(string source, string destination, Log log)
        {
            var destinationFolder = Path.GetDirectoryName(destination);

            try
            {
                if (!string.IsNullOrEmpty(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                File.Copy(source, destination, overwrite: true);
                log.WriteLine($"Copy {source} to {destination}");
            }
            catch (Exception ex)
            {
                log.WriteError($"Unable to copy {source} to {destination}: {ex.Message}");
                return false;
            }

            return true;
        }

        public static bool DeleteFile(string deletedFilePath, Log log)
        {
            try
            {
                // this can happen if the directory contents cache is out-of-date
                if (!File.Exists(deletedFilePath))
                {
                    return true;
                }

                var attributes = File.GetAttributes(deletedFilePath);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(deletedFilePath, attributes & ~FileAttributes.ReadOnly);
                }

                File.Delete(deletedFilePath);
                log.WriteLine("Delete " + deletedFilePath);
            }
            catch (Exception ex)
            {
                log.WriteError($"Unable to delete file {deletedFilePath}: {ex.Message}");
                return false;
            }

            return true;
        }

        public static bool CreateDirectory(string newFolder, Log log)
        {
            try
            {
                Directory.CreateDirectory(newFolder);
                log.WriteLine("Create " + newFolder);
            }
            catch (Exception ex)
            {
                log.WriteError($"Unable to create directory {newFolder}: {ex.Message}");
                return false;
            }

            return true;
        }

        public static bool DeleteDirectory(string deletedFolderPath, Log log)
        {
            try
            {
                Directory.Delete(deletedFolderPath, recursive: true);
                log.WriteLine("Delete " + deletedFolderPath);
            }
            catch (Exception ex)
            {
                log.WriteError($"Unable to delete directory {deletedFolderPath}: {ex.Message}");
                return false;
            }

            return true;
        }
    }
}
