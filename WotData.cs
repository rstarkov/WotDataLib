using System;
using WotDataLib.Lingo;

namespace WotDataLib
{
    /// <summary>Exposes the features available through WotDataLib.</summary>
    public static class WotData
    {
        /// <summary>
        ///     Gets or sets the translation to use for various messages produced by this library. Note that at this point,
        ///     the library is not fully translatable.</summary>
        public static Translation Translation
        {
            get { return _translation; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();
                _translation = value;
            }
        }
        private static Translation _translation = new Translation();

        /// <summary>
        ///     Loads all data from the disk and resolves it as it applies to the specified game installation.</summary>
        /// <param name="dataPath">
        ///     Path to the WotDataLib data files, specifically WotBasic-*, WotData-* and WotGameVersion-*.</param>
        /// <param name="installation">
        ///     Game installation for which the data is to be loaded.</param>
        /// <param name="defaultAuthor">
        ///     Where a property is defined by more than one author and accessed by name alone, specifies the name of the
        ///     preferred author to be used.</param>
        public static WotContext Load(string dataPath, GameInstallation installation, string defaultAuthor)
        {
            return WotDataLoader.Load(dataPath, installation, defaultAuthor);
        }
    }
}
