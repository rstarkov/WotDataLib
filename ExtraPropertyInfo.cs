using System;
using System.Collections.Generic;
using RT.Util.ExtensionMethods;

namespace WotDataLib
{
    /// <summary>Describes an "extra" property.</summary>
    public class ExtraPropertyInfo
    {
        /// <summary>Identifies the property that this information is about. Not null.</summary>
        public ExtraPropertyId PropertyId { get; private set; }
        /// <summary>
        ///     Optional; empty if no descriptions are available. Not null. Key is a language code, value is the description
        ///     in that language.</summary>
        public IDictionary<string, string> Descriptions { get; private set; }

        public ExtraPropertyInfo(ExtraPropertyId id, IDictionary<string, string> descriptions)
        {
            if (id == null)
                throw new ArgumentNullException();
            if (descriptions == null)
                throw new ArgumentNullException();
            PropertyId = id;
            Descriptions = descriptions.AsReadOnly();
        }
    }
}
