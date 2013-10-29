using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RT.Util.Xml;

namespace WotDataLib
{
    /// <summary>
    ///     Describes a specific game installation location, and exposes information about which version of the game client is
    ///     located at this path.</summary>
    public class GameInstallation : IComparable<GameInstallation>
    {
        /// <summary>Absolute path to the root of this game installation.</summary>
        public string Path { get { return _path; } }
        private string _path;

        /// <summary>
        ///     A machine-comparable game version identifier, aka "build ID". Null if the path does not exist or does not
        ///     appear to contain a supported game installation.</summary>
        [XmlIgnore]
        public int? GameVersionId { get; private set; }

        /// <summary>A human-readable name of the game version. Null iff the <see cref="GameVersionId"/> is null.</summary>
        [XmlIgnore]
        public string GameVersionName { get; private set; }

        protected GameInstallation() { } // for XmlClassify

        /// <summary>Constructor. Analyses the specified path and determines which game version is installed there (if any).</summary>
        public GameInstallation(string path)
        {
            _path = path;
            try
            {
                var xml = XDocument.Parse(File.ReadAllText(System.IO.Path.Combine(path, "version.xml")));
                var version = xml.Root.Element("version");

                var m = Regex.Match(version.Value, @"^\s*(v\.)?(?<name>.*?)\s+#(?<build>\d+)(?<idiotic_suffix>.*?)\s*$");
                if (!m.Success)
                    throw new WotDataUserError("Cannot parse version string: " + version.Value);

                GameVersionId = int.Parse(m.Groups["build"].Value);
                GameVersionName = m.Groups["name"].Value;
            }
            catch
            {
                GameVersionId = null;
                GameVersionName = null;
            }
        }

        /// <summary>Re-analyses the installation path and refreshes information about the game version installed there.</summary>
        public virtual void Reload()
        {
            var loaded = new GameInstallation(_path);
            GameVersionId = loaded.GameVersionId;
            GameVersionName = loaded.GameVersionName;
        }

        public override string ToString() { return (GameVersionName ?? "?") + ":  " + Path; }

        public int CompareTo(GameInstallation other)
        {
            if (other == null) return 1;
            if (GameVersionId == null && other.GameVersionId == null)
                return 0;
            if (GameVersionId == null || other.GameVersionId == null)
                return other.GameVersionId == null ? -1 : 1;
            int result = -GameVersionId.Value.CompareTo(other.GameVersionId.Value);
            if (result != 0)
                return result;
            return string.Compare(Path, other.Path);
        }
    }
}
