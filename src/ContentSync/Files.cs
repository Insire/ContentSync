﻿using System.IO;
using System.Threading;

namespace GuiLabs.FileUtilities
{
    public static class Files
    {
        private const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;

        /// <summary>
        /// Assumes both files already exist.
        /// </summary>
        public static bool AreContentsIdentical(string filePath1, string filePath2, CancellationToken token)
        {
            var fileInfo1 = new FileInfo(filePath1);
            var fileInfo2 = new FileInfo(filePath2);

            if (fileInfo1.Length != fileInfo2.Length)
            {
                return false;
            }

            const int bufferSize = 4096;
            byte[] buffer1 = new byte[bufferSize];
            byte[] buffer2 = new byte[bufferSize];

            using (var stream1 = new FileStream(filePath1, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan | FileFlagNoBuffering))
            using (var stream2 = new FileStream(filePath2, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan | FileFlagNoBuffering))
            {
                while (stream1.Position < stream1.Length)
                {
                    if (token.IsCancellationRequested)
                        break;

                    int bytesRead1 = stream1.Read(buffer1, 0, bufferSize);
                    int bytesRead2 = stream2.Read(buffer2, 0, bufferSize);
                    if (bytesRead1 != bytesRead2)
                    {
                        // can this ever happen?
                        return false;
                    }

                    for (int i = 0; i < bytesRead1; i++)
                    {
                        if (buffer1[i] != buffer2[i])
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
