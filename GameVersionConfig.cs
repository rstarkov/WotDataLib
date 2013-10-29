﻿using System.Collections.Generic;
using RT.Util.Xml;

namespace WotDataLib
{
    /// <summary>Holds various tweakable properties which might change between game versions.</summary>
    public sealed class GameVersionConfig
    {
        /// <summary>
        ///     These properties first apply in the game whose build ID is this. This property is deduced from file name,
        ///     rather than its content.</summary>
        [XmlIgnore]
        public int GameVersionId { get; internal set; }

        /// <summary>Relative path to the root directory containing modding-related files for this specific version.</summary>
        public string PathMods { get; private set; }
        /// <summary>Path to the BXML file listing which vehicles are defined in the game client data files.</summary>
        public string PathVehicleList { get; private set; }
        /// <summary>Path to the MO files, which contain strings used in the game client.</summary>
        public string PathMoFiles { get; private set; }
        /// <summary>Relative path to the directory containing the tank icons we're creating.</summary>
        public string PathDestination { get; private set; }
        /// <summary>
        ///     Relative path to the directory containing contour tank images. May refer to a zip file with a colon separating
        ///     the path within the zip.</summary>
        public string PathSourceContour { get; private set; }
        /// <summary>
        ///     Relative path to the directory containing 3D tank images. May refer to a zip file with a colon separating the
        ///     path within the zip.</summary>
        public string PathSource3D { get; private set; }
        /// <summary>
        ///     Relative path to the directory containing 3D (large) tank images. May refer to a zip file with a colon
        ///     separating the path within the zip.</summary>
        public string PathSource3DLarge { get; private set; }
        public Dictionary<Country, string> PathSourceCountry { get; private set; }
        public Dictionary<Class, string> PathSourceClass { get; private set; }

        /// <summary>Specifies whether the tank images should be loaded and saved as PNG or TGA.</summary>
        public string TankIconExtension { get; private set; }

        /// <summary>Constructor, for use by XmlClassify.</summary>
        private GameVersionConfig() { }
    }

}