using System.Collections.Generic;
using System.Linq;
using RT.Util.Lingo;

namespace WotDataLib
{
    /// <summary>
    ///     Provides access to the World of Tanks data as it applies to a specific game version, and exposes a list of any
    ///     warnings issued while loading the data.</summary>
    public sealed class WotContext
    {
        internal WotContext(GameInstallation installation, GameVersionConfig versionConfig, IEnumerable<string> warnings, string defaultAuthor)
        {
            Installation = installation;
            VersionConfig = versionConfig;
            Warnings = warnings.ToList();
            Tanks = new List<WotTank>();
            ExtraProperties = new List<ExtraPropertyInfo>();
            DefaultAuthor = defaultAuthor;
        }

        /// <summary>
        ///     Gets a list of warnings issued while loading the data. These can be serious and help understand why data might
        ///     be missing. This list is read-only.</summary>
        public IList<string> Warnings { get; private set; }

        /// <summary>Gets information about the game installation that this context is for.</summary>
        public GameInstallation Installation { get; private set; }

        /// <summary>
        ///     Provides access to various configuration settings specific to the game version that this context is for. Is
        ///     null if no suitable version config could be found.</summary>
        public GameVersionConfig VersionConfig { get; private set; }

        /// <summary>Gets all the tank data applicable in this context. This list is read-only.</summary>
        public IList<WotTank> Tanks { get; private set; }

        /// <summary>Gets a list of all the extra properties applicable in this context. This list is read-only.</summary>
        public IList<ExtraPropertyInfo> ExtraProperties { get; private set; }

        /// <summary>
        ///     Gets the property author to be used by default when the author is not specified and there is more than one to
        ///     choose from. This is done automatically when accessing the tank's extra properties through the indexer.</summary>
        public string DefaultAuthor { get; private set; }

        internal void Freeze()
        {
            Warnings = Warnings.ToList().AsReadOnly();
            Tanks = Tanks.ToList().AsReadOnly();
            ExtraProperties = ExtraProperties.ToList().AsReadOnly();
        }
    }
}
