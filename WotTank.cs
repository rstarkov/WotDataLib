using System.Collections.Generic;
using System.Linq;
using RT.Util.ExtensionMethods;

namespace WotDataLib
{
    /// <summary>Encapsulates all the available information about a tank as it applies to a specific game version.</summary>
    public class WotTank
    {
        /// <summary>WoT tank identifier. Consists of the tank's nation plus a unique string.</summary>
        public string TankId { get; protected set; }
        /// <summary>Tank's country.</summary>
        public Country Country { get; protected set; }
        /// <summary>Tank's tier.</summary>
        public int Tier { get; protected set; }
        /// <summary>Tank's class: light/medium/heavy tank, artillery or tank destroyer.</summary>
        public Class Class { get; protected set; }
        /// <summary>Tank's availability: normal (buyable for silver), premium (buyable for gold), or special (not for sale).</summary>
        public Category Category { get; protected set; }

        /// <summary>Gets the context that this tank info belongs to.</summary>
        public WotContext Context { get; private set; }

        /// <summary>
        ///     Gets the raw client data for this tank. Note that this information cannot be overridden using CSV files; all
        ///     values come directly from the game client data.</summary>
        public WdTank ClientData { get; internal set; }

        private Dictionary<ExtraPropertyId, string> _extras;

        private WotTank() { }

        public WotTank(string tankId, Country country, int tier, Class class_, Category category, IEnumerable<KeyValuePair<ExtraPropertyId, string>> extras, WotContext context)
        {
            TankId = tankId;
            Country = country;
            Tier = tier;
            Class = class_;
            Category = category;
            Context = context;
            _extras = extras.ToDictionary();
        }

        public WotTank(WotTank tank)
        {
            TankId = tank.TankId;
            Country = tank.Country;
            Tier = tank.Tier;
            Class = tank.Class;
            Category = tank.Category;
            Context = tank.Context;
            _extras = tank._extras;
        }

        /// <summary>
        ///     Gets the value of an "extra" property. If the referenced property doesn't exist, a null value is returned.</summary>
        public virtual string this[ExtraPropertyId property]
        {
            get
            {
                if (property == null)
                    return null;
                if (property == ExtraPropertyId.TierArabic)
                    return Tier == 0 ? "" : Tier.ToString();
                else if (property == ExtraPropertyId.TierRoman)
                    return WdUtil.RomanNumerals[Tier].ToString();
                string result;
                if (!_extras.TryGetValue(property, out result))
                    return null;
                return result;
            }
        }

        /// <summary>
        ///     Gets the value of an "extra" property by property name. Acceptable values are FileId combined with ColumnId,
        ///     where present, slash-separated, plus optionally the author (also slash-separated). If no matching property can
        ///     be found, a null value is returned. When the author is omitted and several properties match, preference will
        ///     be given to the <see cref="WotContext.DefaultAuthor"/>.</summary>
        public virtual string this[string name]
        {
            get
            {
                if (name == null)
                    return null;
                var matches = _extras.Keys.Where(k =>
                        (k.FileId + (k.ColumnId == null ? "" : ("/" + k.ColumnId))).EqualsNoCase(name) ||
                        k.ToString().EqualsNoCase(name)
                    ).ToArray();
                // There can only be more than one match if the property was specified by name only and we have more than one author's variant of that property
                if (matches.Length == 0)
                    return null;
                if (matches.Length == 1)
                    return _extras[matches[0]];
                var match = matches.FirstOrDefault(k => k.Author.EqualsNoCase(Context.DefaultAuthor));
                if (match != null) return _extras[match];
                return _extras[matches[0]];
            }
        }

        /// <summary>For debugging.</summary>
        public override string ToString()
        {
            return "Tank: " + TankId;
        }
    }
}
