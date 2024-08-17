using System.Collections.Generic;

namespace GameX
{
    public static class BaseExtensions
    {
        internal static IEnumerable<string> FindPaths(this IFileSystem fileSystem, string path, string searchPattern)
        {
            // expand
            int expandStartIdx, expandMidIdx, expandEndIdx;
            if ((expandStartIdx = searchPattern.IndexOf('(')) != -1 &&
                (expandMidIdx = searchPattern.IndexOf(':', expandStartIdx)) != -1 &&
                (expandEndIdx = searchPattern.IndexOf(')', expandMidIdx)) != -1 &&
                expandStartIdx < expandEndIdx)
            {
                foreach (var expand in searchPattern.Substring(expandStartIdx + 1, expandEndIdx - expandStartIdx - 1).Split(':'))
                    foreach (var found in FindPaths(fileSystem, path, searchPattern.Remove(expandStartIdx, expandEndIdx - expandStartIdx + 1).Insert(expandStartIdx, expand)))
                        yield return found;
                yield break;
            }
            foreach (var file in fileSystem.Glob(path, searchPattern)) yield return file;
        }
    }
}
