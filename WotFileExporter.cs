using System;
using System.Diagnostics.Contracts;
using System.Text;
using RT.Util;

namespace WotDataLib
{
    using System.IO;
    using ICSharpCode.SharpZipLib.Zip;

    static class WotFileExporter
    {
        /// <summary>
        /// Retrives file Stream for normal path or path inside of .pkg (like scripts.pkg|scripts...)
        /// </summary>
        /// <param name="path">Path to file</param>
        /// <returns>File stream</returns>
        public static Stream GetFileStream(string path)
        {
            if (path.Contains("|"))
            {
                return ZipCache.GetZipFileStream(path);
            }
            else
            {
                return File.OpenRead(path);
            }
        }

        /// <summary>
        /// File.Exist with zipped file support
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool Exists(string path)
        {
            if (path.Contains("|"))
            {
                var paths = path.Split('|');
                if (!File.Exists(paths[0]))
                {
                    return false;
                }
                var zip = new ZipFile(paths[0]);
                var entry = zip.GetEntry(paths[1].Replace('\\', '/'));
                return entry != null;
            }
            else
            {
                return File.Exists(path);
            }
        }

        /// <summary>
        /// Port of Path.Combine without illegal chars check
        /// </summary>
        /// <param name="paths">An array of paths to combine</param>
        /// <returns>Combined path</returns>
        public static string CombinePaths(params String[] paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException("paths");
            }

            int finalSize = 0;
            int firstComponent = 0;

            // We have two passes, the first calcuates how large a buffer to allocate and does some precondition
            // checks on the paths passed in.  The second actually does the combination.

            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i] == null)
                {
                    throw new ArgumentNullException("paths");
                }

                if (paths[i].Length == 0)
                {
                    continue;
                }

                //CheckInvalidPathChars(paths[i]);

                if (WotFileExporter.IsPathRooted(paths[i]))
                {
                    firstComponent = i;
                    finalSize = paths[i].Length;
                }
                else
                {
                    finalSize += paths[i].Length;
                }

                char ch = paths[i][paths[i].Length - 1];
                if (ch != Path.DirectorySeparatorChar && ch != Path.AltDirectorySeparatorChar && ch != Path.VolumeSeparatorChar && ch != '|')
                    finalSize++;
            }

            StringBuilder finalPath = new StringBuilder(finalSize);

            for (int i = firstComponent; i < paths.Length; i++)
            {
                if (paths[i].Length == 0)
                {
                    continue;
                }

                if (finalPath.Length == 0)
                {
                    finalPath.Append(paths[i]);
                }
                else
                {
                    char ch = finalPath[finalPath.Length - 1];
                    if (ch != Path.DirectorySeparatorChar && ch != Path.AltDirectorySeparatorChar && ch != Path.VolumeSeparatorChar && ch != '|')
                    {
                        finalPath.Append(Path.DirectorySeparatorChar);
                    }

                    finalPath.Append(paths[i]);
                }
            }

            return finalPath.ToString();
        }

        public static bool IsPathRooted(String path)
        {
            if (path != null)
            {
                int length = path.Length;
                if ((length >= 1 &&
                     (path[0] == Path.DirectorySeparatorChar || path[0] == Path.AltDirectorySeparatorChar)) ||
                    (length >= 2 && path[1] == Path.VolumeSeparatorChar))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
