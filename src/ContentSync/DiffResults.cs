using System.Collections.Generic;
using System.Linq;

namespace GuiLabs.FileUtilities
{
    public class FolderDiffResults
    {
        public IEnumerable<string> LeftOnlyFiles { get; }
        public IEnumerable<string> LeftOnlyFolders { get; }
        public IEnumerable<string> IdenticalFiles { get; }
        public IEnumerable<string> ChangedFiles { get; }
        public IEnumerable<string> RightOnlyFiles { get; }
        public IEnumerable<string> RightOnlyFolders { get; }

        public FolderDiffResults(
            IEnumerable<string> leftOnlyFiles,
            IEnumerable<string> identicalFiles,
            IEnumerable<string> changedFiles,
            IEnumerable<string> rightOnlyFiles,
            IEnumerable<string> leftOnlyFolders,
            IEnumerable<string> rightOnlyFolders)
        {
            LeftOnlyFiles = leftOnlyFiles;
            IdenticalFiles = identicalFiles;
            ChangedFiles = changedFiles;
            RightOnlyFiles = rightOnlyFiles;
            LeftOnlyFolders = leftOnlyFolders;
            RightOnlyFolders = rightOnlyFolders;
        }

        public bool AreFullyIdentical
        {
            get
            {
                return
                    !LeftOnlyFiles.Any()
                    && !RightOnlyFiles.Any()
                    && !ChangedFiles.Any()
                    && !LeftOnlyFolders.Any()
                    && !RightOnlyFolders.Any();
            }
        }
    }
}
