﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RT.Util;
using RT.Util.Collections;
using RT.Util.ExtensionMethods;
using RT.Util.Json;
using RT.Util.Lingo;
using RT.Util.Xml;

namespace WotDataLib
{
    static class WotDataLoader
    {
        public static WotContext Load(string dataPath, GameInstallation installation, string defaultAuthor)
        {
            if (installation.GameVersionId == null)
                throw new WotDataUserError("No supported game version detected at the specified installation path.");

            var warnings = new List<string>();
            if (!Directory.Exists(dataPath))
                return null; // because there are no game version configs available

            var versionConfigs = loadGameVersionConfig(dataPath, warnings);
            var versionConfig = versionConfigs.Where(v => v.GameVersionId <= installation.GameVersionId.Value).MaxElementOrDefault(v => v.GameVersionId);
            // versionConfig may be null here

            var clientData = loadFromClient(installation, versionConfig, warnings);
            var builtin = loadBuiltInFiles(dataPath, warnings, clientData.Item1);
            var extras = loadDataExtraFiles(dataPath, warnings, clientData.Item2);

            if (!builtin.Any() || !versionConfigs.Any())
                warnings.Add(WdUtil.Tr.Error.DataDir_NoFilesAvailable);

            return resolve(installation, versionConfig, builtin, extras, warnings, defaultAuthor);
        }

        private static List<GameVersionConfig> loadGameVersionConfig(string dataPath, List<string> warnings)
        {
            var result = new List<GameVersionConfig>();
            foreach (var fi in new DirectoryInfo(dataPath).GetFiles("WotGameVersion-*.xml"))
            {
                var parts = fi.Name.Substring(0, fi.Name.Length - 4).Split('-');

                if (parts.Length != 2)
                {
                    warnings.Add(WdUtil.Tr.Error.DataDir_Skip_WrongParts.Fmt(fi.Name, "2", parts.Length));
                    continue;
                }

                int gameVersionId;
                if (!parts[1].StartsWith("#") || !int.TryParse(parts[1].Substring(1), out gameVersionId))
                {
                    warnings.Add(WdUtil.Tr.Error.DataDir_Skip_GameVersion.Fmt(fi.Name, parts[1]));
                    continue;
                }

                try
                {
                    var ver = XmlClassify.LoadObjectFromXmlFile<GameVersionConfig>(fi.FullName);
                    ver.GameVersionId = gameVersionId;
                    result.Add(ver);
                }
                catch (Exception e)
                {
                    warnings.Add(WdUtil.Tr.Error.DataDir_Skip_FileError.Fmt(fi.Name, e.Message));
                    continue;
                }
            }
            return result;
        }

        private static Tuple<unresolvedBuiltIn, List<unresolvedExtraFileCol>> loadFromClient(GameInstallation installation, GameVersionConfig versionConfig, List<string> warnings)
        {
            var splitStringRef = Ut.Lambda((string str) =>
            {
                var split = str.Split(':');
                if (split.Length != 2) throw new WotDataException("1618441");
                if (!split[0].StartsWith("#")) throw new WotDataException("20891");
                return new { file = split[0].Substring(1), key = split[1] };
            });

            var countries = new[] { "ussr", "germany", "usa", "france", "china", "uk", "japan" };
            countries = countries.Where(c => File.Exists(Path.Combine(installation.Path, versionConfig.PathVehicleList.Replace(@"""Country""", c)))).ToArray();

            var tanks = countries.SelectMany(country =>
                BxmlReader.ReadFile(Path.Combine(installation.Path, versionConfig.PathVehicleList.Replace(@"""Country""", country)))
                .GetDict()
                .Select(kvp => new { country, id = kvp.Key, raw = kvp.Value })
            ).ToDictionary(tank => tank.country + "-" + tank.id, tank =>
            {
                var tags1 = tank.raw["tags"].GetString().Split("\r\n").Select(s => s.Trim()).ToHashSet();
                var tags2 = tank.raw["tags"].GetString().Split(' ').Select(s => s.Trim()).ToHashSet();
                var tags = tags1.Count > tags2.Count ? tags1 : tags2;
                bool notInShop = tank.raw.ContainsKey("notInShop") && tank.raw["notInShop"].GetBool();
                bool gold = tank.raw["price"] is JsonDict && tank.raw["price"].ContainsKey("gold");
                var advancedInfo = BxmlReader.ReadFile(Path.Combine(installation.Path, @"res\scripts\item_defs\vehicles\{0}\{1}.xml".Fmt(tank.country, tank.id)));

                Country country;
                switch (tank.country)
                {
                    case "ussr": country = Country.USSR; break;
                    case "germany": country = Country.Germany; break;
                    case "usa": country = Country.USA; break;
                    case "france": country = Country.France; break;
                    case "china": country = Country.China; break;
                    case "uk": country = Country.UK; break;
                    case "japan": country = Country.Japan; break;
                    default:
                        warnings.Add("Unknown country in game data: " + tank.country);
                        return null;
                }

                Class class_;
                if (tags.Contains("lightTank"))
                    class_ = Class.Light;
                else if (tags.Contains("mediumTank"))
                    class_ = Class.Medium;
                else if (tags.Contains("heavyTank"))
                    class_ = Class.Heavy;
                else if (tags.Contains("SPG"))
                    class_ = Class.Artillery;
                else if (tags.Contains("AT-SPG"))
                    class_ = Class.Destroyer;
                else
                {
                    warnings.Add("Unknown tank class in game data; tags: " + tags.JoinString(", "));
                    return null;
                }

                return new
                {
                    tank.id,
                    country,
                    tank.raw,
                    nameFull = splitStringRef(tank.raw["userString"].GetString()),
                    nameShort = splitStringRef(tank.raw.ContainsKey("shortUserString") ? tank.raw["shortUserString"].GetString() : tank.raw["userString"].GetString()),
                    tier = tank.raw["level"].GetInt(),
                    secret = tags.Contains("secret"),
                    tags,
                    notInShop,
                    gold,
                    kind = notInShop ? Category.Special : gold ? Category.Premium : Category.Normal,
                    class_,
                    speedForward = advancedInfo["speedLimits"]["forward"].GetDecimalLenient(),
                    speedReverse = advancedInfo["speedLimits"]["backward"].GetDecimalLenient(),
                };
            });

            tanks.RemoveAllByValue(tank => tank == null);

            var moFiles = tanks.Select(kvp => kvp.Value.nameFull.file).Concat(tanks.Select(kvp => kvp.Value.nameShort.file)).Distinct()
                .ToDictionary(filename => filename, filename => MoReader.ReadFile(Path.Combine(installation.Path, versionConfig.PathMoFiles, filename + ".mo")));

            // Generate data

            //// Built-in
            var builtin = new unresolvedBuiltIn();
            builtin.FileVersion = 0;
            builtin.Entries = tanks.Select(tank =>
                new TankEntry(tank.Key, tank.Value.country, tank.Value.tier, tank.Value.class_, tank.Value.kind, installation.GameVersionId, false)
            ).ToList();


            //// NameFull / Wargaming
            var nameFull = new unresolvedExtraFileCol();
            nameFull.PropertyId = new ExtraPropertyId("NameFull", null, "Wargaming");
            nameFull.FileVersion = 0;
            nameFull.Descriptions = new Dictionary<string, string>
            {
                { "Ru", "Полные названия танков, оригинал – как в игре." },
                { "En", "Full tank names, original – like in the game." },
            };
            nameFull.Entries = tanks.Select(tank =>
            {
                try { return new ExtraEntry(tank.Key, moFiles[tank.Value.nameFull.file][tank.Value.nameFull.key], installation.GameVersionId); }
                catch { return new ExtraEntry(tank.Key, "<no name>", installation.GameVersionId); }
            }).ToList();


            //// NameShort / Wargaming
            var nameShort = new unresolvedExtraFileCol();
            nameShort.PropertyId = new ExtraPropertyId("NameShort", null, "Wargaming");
            nameShort.FileVersion = 0;
            nameShort.Descriptions = new Dictionary<string, string>
            {
                { "Ru", "Короткие названия танков, оригинал – как в игре." },
                { "En", "Short tank names, original – like in the game." },
            };
            nameShort.Entries = tanks.Select(tank =>
            {
                try { return new ExtraEntry(tank.Key, moFiles[tank.Value.nameShort.file][tank.Value.nameShort.key], installation.GameVersionId); }
                catch { return new ExtraEntry(tank.Key, "<no name>", installation.GameVersionId); }
            }).ToList();


            //// Speed: forward
            var speedForward = new unresolvedExtraFileCol();
            speedForward.PropertyId = new ExtraPropertyId("Speed", "Forward", "Wargaming");
            speedForward.FileVersion = 0;
            speedForward.Descriptions = new Dictionary<string, string>
            {
                { "Ru", "Максимальная скорость вперед." },
                { "En", "Maximum forward speed." },
            };
            speedForward.Entries = tanks.Select(tank =>
                new ExtraEntry(tank.Key, tank.Value.speedForward.ToString(), installation.GameVersionId)
            ).ToList();


            //// Speed: reverse
            var speedReverse = new unresolvedExtraFileCol();
            speedReverse.PropertyId = new ExtraPropertyId("Speed", "Reverse", "Wargaming");
            speedReverse.FileVersion = 0;
            speedReverse.Descriptions = new Dictionary<string, string>
            {
                { "Ru", "Максимальная скорость назад." },
                { "En", "Maximum reverse speed." },
            };
            speedReverse.Entries = tanks.Select(tank =>
                new ExtraEntry(tank.Key, tank.Value.speedReverse.ToString(), installation.GameVersionId)
            ).ToList();


            return Tuple.Create(builtin, new List<unresolvedExtraFileCol> { nameFull, nameShort, speedForward, speedReverse });
        }

        private static Dictionary<string, List<TankEntry>> loadBuiltInFiles(string dataPath, List<string> warnings, unresolvedBuiltIn fromClient)
        {
            var builtin = new List<unresolvedBuiltIn>();
            builtin.Add(fromClient);
            var origFilenames = new Dictionary<object, string>();
            foreach (var fi in new DirectoryInfo(dataPath).GetFiles("WotBuiltIn-*.csv"))
            {
                var parts = fi.Name.Substring(0, fi.Name.Length - 4).Split('-');

                if (parts.Length != 2)
                {
                    warnings.Add(WdUtil.Tr.Error.DataDir_Skip_WrongParts.Fmt(fi.Name, "2", parts.Length));
                    continue;
                }

                int fileVersion;
                if (!int.TryParse(parts[1], out fileVersion) || fileVersion < 1)
                {
                    warnings.Add(WdUtil.Tr.Error.DataDir_Skip_FileVersion.Fmt(fi.Name, parts[1]));
                    continue;
                }

                try
                {
                    var df = loadBuiltInFile(fileVersion, fi.FullName);
                    builtin.Add(df);
                    origFilenames[df] = fi.Name;
                }
                catch (Exception e)
                {
                    warnings.Add(WdUtil.Tr.Error.DataDir_Skip_FileError.Fmt(fi.Name, e.Message));
                    continue;
                }
            }

            return resolveBuiltIns(warnings, builtin, origFilenames);
        }

        private static List<resolvedExtraProperty> loadDataExtraFiles(string dataPath, List<string> warnings, List<unresolvedExtraFileCol> clientData)
        {
            var extra = new List<unresolvedExtraFileCol>();
            extra.AddRange(clientData);
            var origFilenames = new Dictionary<object, string>();
            foreach (var fi in new DirectoryInfo(dataPath).GetFiles("WotData-*.csv"))
            {
                var parts = fi.Name.Substring(0, fi.Name.Length - 4).Split('-');

                if (parts.Length != 4)
                {
                    warnings.Add(WdUtil.Tr.Error.DataDir_Skip_WrongParts.Fmt(fi.Name, "4", parts.Length));
                    continue;
                }

                int fileVersion;
                if (!int.TryParse(parts[3], out fileVersion) || fileVersion < 1)
                {
                    warnings.Add(WdUtil.Tr.Error.DataDir_Skip_FileVersion.Fmt(fi.Name, parts[3]));
                    continue;
                }

                string name = parts[1].Trim();
                if (name.Length == 0)
                {
                    warnings.Add(WdUtil.Tr.Error.DataDir_Skip_PropName.Fmt(fi.Name));
                    continue;
                }

                string author = parts[2].Trim();
                if (author.Length == 0)
                {
                    warnings.Add(WdUtil.Tr.Error.DataDir_Skip_Author.Fmt(fi.Name));
                    continue;
                }

                try
                {
                    var df = loadExtraFile(name, author, fileVersion, fi.FullName);
                    foreach (var prop in df)
                    {
                        extra.Add(prop);
                        origFilenames[prop] = fi.Name;
                    }
                }
                catch (Exception e)
                {
                    warnings.Add(WdUtil.Tr.Error.DataDir_Skip_FileError.Fmt(fi.Name, e.Message));
                    continue;
                }
            }

            return resolveExtras(warnings, extra, origFilenames);
        }

        private static HashSet<string> _languages = EnumStrong.GetValues<Language>().Select(l => l.GetIsoLanguageCode()).Concat("en").ToHashSet(StringComparer.OrdinalIgnoreCase);

        private static unresolvedBuiltIn loadBuiltInFile(int fileVersion, string filename)
        {
            var result = new unresolvedBuiltIn();
            result.FileVersion = fileVersion;

            var lines = WdUtil.ReadCsvLines(filename).ToArray();
            if (lines.Length == 0)
                throw new WotDataUserError(WdUtil.Tr.Error.DataFile_EmptyFile);
            var header = lines[0].Item2;
            if (header.Length < 2)
                throw new WotDataUserError(WdUtil.Tr.Error.DataFile_TooFewFieldsFirstLine);
            if (header[0] != "WOT-BUILTIN")
                throw new WotDataUserError(WdUtil.Tr.Error.DataFile_ExpectedSignature.Fmt("WOT-BUILTIN"));
            if (header[1] != "2")
                throw new WotDataUserError(WdUtil.Tr.Error.DataFile_ExpectedV2);

            result.Entries = lines.Skip(1).Select(lp =>
            {
                try
                {
                    var fields = lp.Item2;
                    if (fields.Length < 1)
                        throw new WotDataUserError(WdUtil.Tr.Error.DataFile_TooFewFields.Fmt(WdUtil.Tr, 1));

                    string tankId = fields[0];
                    int? tier = null;
                    Country? country = null;
                    Class? class_ = null;
                    Category? category = null;
                    int? gameVersionId = null;
                    bool delete = false;

                    if (fields.Length > 1 && fields[1] != "")
                        switch (fields[1])
                        {
                            case "ussr": country = Country.USSR; break;
                            case "germany": country = Country.Germany; break;
                            case "usa": country = Country.USA; break;
                            case "china": country = Country.China; break;
                            case "france": country = Country.France; break;
                            case "uk": country = Country.UK; break;
                            case "none": country = Country.None; break;
                            default: throw new WotDataUserError(WdUtil.Tr.Error.DataFile_UnrecognizedCountry.Fmt(fields[1],
                                new[] { "ussr", "germany", "usa", "china", "france", "uk", "none" }.JoinString(", ", "\"", "\"")));
                        }

                    if (fields.Length > 2 && fields[2] != "")
                    {
                        int tierNN;
                        if (!int.TryParse(fields[2], out tierNN))
                            throw new WotDataUserError(string.Format(WdUtil.Tr.Error.DataFile_TankTierValue, fields[2]));
                        if (tierNN < 0 || tierNN > 10)
                            throw new WotDataUserError(string.Format(WdUtil.Tr.Error.DataFile_TankTierValue, fields[2]));
                        tier = tierNN;
                    }

                    if (fields.Length > 3 && fields[3] != "")
                        switch (fields[3])
                        {
                            case "light": class_ = Class.Light; break;
                            case "medium": class_ = Class.Medium; break;
                            case "heavy": class_ = Class.Heavy; break;
                            case "destroyer": class_ = Class.Destroyer; break;
                            case "artillery": class_ = Class.Artillery; break;
                            case "none": class_ = Class.None; break;
                            default: throw new WotDataUserError(WdUtil.Tr.Error.DataFile_UnrecognizedClass.Fmt(fields[3],
                                new[] { "light", "medium", "heavy", "destroyer", "artillery", "none" }.JoinString(", ", "\"", "\"")));
                        }

                    if (fields.Length > 4 && fields[4] != "")
                        switch (fields[4])
                        {
                            case "normal": category = Category.Normal; break;
                            case "premium": category = Category.Premium; break;
                            case "special": category = Category.Special; break;
                            default: throw new WotDataUserError(WdUtil.Tr.Error.DataFile_UnrecognizedCategory.Fmt(fields[4],
                                new[] { "normal", "premium", "special" }.JoinString(", ", "\"", "\"")));
                        }

                    if (fields.Length > 5 && fields[5] != "")
                    {
                        int gameVerId;
                        if (!fields[5].StartsWith('#') || !int.TryParse(fields[5].Substring(1), out gameVerId))
                            throw new WotDataUserError("The game version doesn't start with a # or does not parse as a number.");
                        gameVersionId = gameVerId;
                    }

                    if (fields.Length > 6)
                    {
                        if (fields[6] == "del")
                            delete = true;
                        else if (fields[6] != "")
                            throw new WotDataUserError("The very last column must contain the text \"del\" or nothing.");
                    }

                    return new TankEntry(tankId, country, tier, class_, category, gameVersionId, delete);
                }
                catch (Exception e)
                {
                    throw new WotDataUserError(WdUtil.Tr.Error.DataFile_LineNum.Fmt(lp.Item1, e.Message));
                }
            }).ToList();

            return result;
        }

        private static List<unresolvedExtraFileCol> loadExtraFile(string name, string author, int fileVersion, string filename)
        {
            var lines = WdUtil.ReadCsvLines(filename).ToArray();
            var header = lines[0].Item2;
            if (header.Length < 2)
                throw new WotDataUserError(WdUtil.Tr.Error.DataFile_TooFewFieldsFirstLine);
            if (header[0] != "WOT-DATA")
                throw new WotDataUserError(WdUtil.Tr.Error.DataFile_ExpectedSignature.Fmt("WOT-DATA"));
            if (header[1] != "2")
                throw new WotDataUserError(WdUtil.Tr.Error.DataFile_ExpectedV2);

            bool headersDone = false;
            string[] headerInherit = null;
            string[] headerID = null;
            var headerDesc = new Dictionary<string, string[]>();
            var entries = new AutoDictionary<string, List<ExtraEntry>>(_ => new List<ExtraEntry>());
            for (int l = 1; l < lines.Length; l++)
            {
                try
                {
                    var fields = lines[l].Item2;
                    bool isHeader = fields[0].StartsWith("{{");
                    if (isHeader && headersDone)
                        throw new WotDataUserError("Headers must not be mixed with the rest of the data.");
                    if (isHeader)
                    {
                        if (fields[0].EqualsNoCase("{{ID}}"))
                        {
                            if (headerID != null)
                                throw new WotDataUserError("There must be at most one ID header in the file");
                            headerID = fields.Skip(1).ToArray();
                            if (headerID.Any(f => f == ""))
                                throw new WotDataUserError("ID header fields must not be empty");
                            if (headerID.Length == 0)
                                throw new WotDataUserError("If present, the ID header must have at least one value");
                            var ids = headerID.ToHashSet(StringComparer.OrdinalIgnoreCase);
                            if (ids.Count != headerID.Length)
                                throw new WotDataUserError("Duplicate column IDs are not allowed");
                        }
                        else if (fields[0].EqualsNoCase("{{Inherit}}"))
                        {
                            if (headerInherit != null)
                                throw new WotDataUserError("There must be at most one Inherit header in the file");
                            headerInherit = fields.Skip(1).ToArray();
                        }
                        else
                        {
                            if (!_languages.Contains(fields[0].SubstringSafe(2, fields[0].Length - 4)))
                                throw new WotDataUserError("Unrecognized header: " + fields[0] + " (must be {{ID}}, {{Inherit}} or a language code such as {{En}} or {{Ru}} etc.)");
                            headerDesc.Add(fields[0].Replace("{", "").Replace("}", ""), fields.Skip(1).ToArray());
                        }
                    }
                    else
                    {
                        if (!headersDone)
                        {
                            headersDone = true;
                            // interpret the headers now
                            if (headerID == null)
                                headerID = new string[] { "" }; // defines how many data columns (=properties) we expect
                            if (headerInherit != null && headerInherit.Length > headerID.Length)
                                throw new WotDataUserError("The inherit header contains too many columns");
                            if (headerDesc.Values.Any(v => v.Length > headerID.Length))
                                throw new WotDataUserError("One of the description headers contains too many columns");
                        }

                        if (fields.Length > 1 + headerID.Length + 1)
                            throw new WotDataUserError("One of the data rows contains too many columns (for multiple columns, add an {{ID}} header)");
                        int? version = null;
                        if (fields.Length > 1 + headerID.Length)
                        {
                            int ver;
                            if (!fields[1 + headerID.Length].StartsWith("#") || !int.TryParse(fields[1 + headerID.Length].SubstringSafe(1), out ver))
                                throw new WotDataUserError("The game version field must be a number preceded by a #, e.g. #123");
                            version = ver;
                        }
                        for (int i = 0; i < headerID.Length; i++)
                            if (fields.Length > i + 1 && fields[i + 1] != "")
                                entries[headerID[i]].Add(new ExtraEntry(fields[0], fields[i + 1], version));
                    }
                }
                catch (Exception e)
                {
                    throw new WotDataUserError(WdUtil.Tr.Error.DataFile_LineNum.Fmt(lines[l].Item1, e.Message));
                }
            }

            var properties = new List<unresolvedExtraFileCol>();
            for (int i = 0; i < headerID.Length; i++)
            {
                var prop = new unresolvedExtraFileCol();
                prop.PropertyId = new ExtraPropertyId(fileId: name, columnId: headerID[i] == "" ? null : headerID[i], author: author);
                prop.Descriptions = headerDesc
                    .Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value[i] == "" ? null : kvp.Value[i]))
                    .Where(kvp => kvp.Value != null)
                    .ToDictionary(StringComparer.OrdinalIgnoreCase);
                prop.Entries = entries[headerID[i]];
                prop.FileVersion = fileVersion;
                prop.InheritsFrom = (headerInherit == null || headerInherit.Length <= i || headerInherit[i] == "") ? null : headerInherit[i];
                properties.Add(prop);
            }

            return properties;
        }

        private static Dictionary<string, List<TankEntry>> resolveBuiltIns(List<string> warnings, List<unresolvedBuiltIn> builtins, Dictionary<object, string> origFilenames)
        {
            // The following rules apply *per tank*.
            // Within a single file, versioned entries take precedence over unversioned entries. The unversioned entries apply only to
            // game versions before the earliest versioned entry. Only a single unversioned entry is allowed, be it add or delete; otherwise
            // only the last one in the file order is kept (+warning). Also, only a single entry is allowed for each game version, otherwise only
            // the last one is kept (+warning). Redundant entries are also removed (+warning), namely delete entries following another delete
            // (in game version order).
            // With multiple files, an unversioned entry overrides *everything* inherited from the earlier versions, while a versioned entry
            // overrides *everything* from that version onwards.
            var resolved = new AutoDictionary<string, HashSet<TankEntry>>(_ => new HashSet<TankEntry>());
            foreach (var builtin in builtins.OrderBy(b => b.FileVersion))
            {
                // Resolve any instances of the file overriding its own entries, issuing warnings along the way
                foreach (var grp in builtin.Entries.GroupBy(e => e.TankId))
                {
                    // There must be at most one entry per version, and at most one unversioned entry.
                    foreach (var ver in grp.GroupBy(e => e.GameVersionId))
                    {
                        if (grp.Count() > 1)
                        {
                            warnings.Add("WotBuiltIn-{0}: Multiple entries found for tank {1} and game version {2}; all except for the last one will be ignored.".Fmt(builtin.FileVersion, grp.Key, ver.Key == null ? "<unspecified>" : ver.Key.ToString()));
                            var set = ver.SkipLast(1).ToHashSet();
                            builtin.Entries.RemoveAll(e => set.Contains(e));
                        }
                    }
                    // There must not be any redundant "delete" entries
                    foreach (var conseq in grp.OrderBy(e => e.GameVersionId ?? -1).GroupConsecutiveBy(e => e.Delete).Where(c => c.Key && c.Count > 1).ToList())
                    {
                        warnings.Add("WotBuiltIn-{0}: Redundant \"del\" entries for tank {1} found; ignored.".Fmt(builtin.FileVersion, grp.Key));
                        var set = conseq.Skip(1).ToHashSet();
                        builtin.Entries.RemoveAll(e => set.Contains(e));
                    }
                }

                // Apply the file's overrides to the global result
                foreach (var cur in builtin.Entries.OrderBy(b => b.GameVersionId == null ? 0 : 1).ThenBy(b => b.Delete ? 0 : 1).ThenBy(b => b.GameVersionId ?? 0))
                {
                    // Note that due to the OrderBy clause, each of the "if"s below cannot encounter any of the entry types listed below itself belonging to the same file version
                    if (cur.GameVersionId == null && cur.Delete)
                    {
                        resolved[cur.TankId].Clear();
                    }
                    else if (cur.GameVersionId == null && !cur.Delete) // spell out for clarity
                    {
                        resolved[cur.TankId].Clear();
                        resolved[cur.TankId].Add(cur);
                    }
                    else if (cur.GameVersionId != null && cur.Delete)
                    {
                        resolved[cur.TankId].RemoveWhere(e => e.GameVersionId >= cur.GameVersionId);
                        resolved[cur.TankId].Add(cur);
                    }
                    else if (cur.GameVersionId != null && !cur.Delete) // no other options left but for clarity...
                    {
                        resolved[cur.TankId].RemoveWhere(e => e.GameVersionId >= cur.GameVersionId);
                        resolved[cur.TankId].Add(cur);
                    }
                }
            }

            return resolved.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.OrderBy(e => e.GameVersionId ?? -1).ToList()
            );
        }

        private static List<resolvedExtraProperty> resolveExtras(List<string> warnings, List<unresolvedExtraFileCol> extraFileCols, Dictionary<object, string> origFilenames)
        {
            // Extra properties are first resolved the same way built-in properties are. Then inheritance is applied, using the
            // "inherit from" value specified in the highest file version (i.e. later files may override the inheritance source).

            var properties = new Dictionary<string, unresolvedExtraProperty>(StringComparer.OrdinalIgnoreCase);

            foreach (var extraFileColGroup in extraFileCols.GroupBy(ex => ex.PropertyId))
            {
                var property = new unresolvedExtraProperty();
                property.PropertyId = extraFileColGroup.First().PropertyId; // there's got to be at least one, otherwise the group couldn't possibly exist
                property.Descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                property.Entries = new AutoDictionary<string, HashSet<ExtraEntry>>(_ => new HashSet<ExtraEntry>());
                properties.Add(property.PropertyId.ToString(), property);

                foreach (var extraFileCol in extraFileColGroup.OrderBy(e => e.FileVersion))
                {
                    // Resolve any instances of the file overriding its own entries, issuing warnings along the way
                    foreach (var grp in extraFileCol.Entries.GroupBy(e => e.TankId))
                    {
                        // There must be at most one entry per version, and at most one unversioned entry.
                        foreach (var ver in grp.GroupBy(e => e.GameVersionId))
                        {
                            if (ver.Count() > 1)
                            {
                                warnings.Add("WotData-{0}-{1} / {2}: Multiple entries found for tank {3} and game version {4}; all except for the last one will be ignored.".Fmt(extraFileCol.PropertyId.FileId, extraFileCol.FileVersion, extraFileCol.PropertyId.ColumnId, extraFileCol.FileVersion, grp.Key, ver.Key == null ? "<unspecified>" : ver.Key.ToString()));
                                foreach (var entry in ver.SkipLast(1))
                                    extraFileCol.Entries.Remove(entry);
                            }
                        }
                    }

                    // Apply the file's overrides to the global result
                    foreach (var cur in extraFileCol.Entries.OrderBy(b => b.GameVersionId ?? -1))
                    {
                        // Note that due to the OrderBy clause, each of the "if"s below cannot encounter any of the entry types listed below itself belonging to the same file version
                        if (cur.GameVersionId == null)
                        {
                            property.Entries[cur.TankId].Clear();
                            property.Entries[cur.TankId].Add(cur);
                        }
                        else if (cur.GameVersionId != null) // spell out for clarity
                        {
                            property.Entries[cur.TankId].RemoveWhere(e => e.GameVersionId >= cur.GameVersionId);
                            property.Entries[cur.TankId].Add(cur);
                        }
                    }

                    // Override inherits from
                    if (extraFileCol.InheritsFrom != null)
                        property.InheritsFrom = extraFileCol.InheritsFrom == "del" ? null : extraFileCol.InheritsFrom;
                    // Override descriptions
                    foreach (var kvp in extraFileCol.Descriptions)
                        property.Descriptions[kvp.Key] = kvp.Value;
                }
            }

            // Now resolve inheritance

            // First, resolve the immedaite parent of each property that inherits from another one, warn about missing properties
            // and circular references, and remove all properties that cannot be resolved.
            while (true)
            {
                // Remove properties which inherit from a non-existent property
                var ignore = new HashSet<unresolvedExtraProperty>();
                do
                {
                    ignore.Clear();
                    foreach (var p in properties.Values.Where(p => p.InheritsFrom != null))
                    {
                        if (!properties.ContainsKey(p.InheritsFrom))
                        {
                            warnings.Add(WdUtil.Tr.Error.DataDir_Skip_InhNoProp.Fmt("<?>"/*origFilenames[p] broken... pls fix me*/, p.InheritsFrom));
                            ignore.Add(p);
                            continue;
                        }
                    }
                    properties.RemoveAllByValue(f => ignore.Contains(f));
                } while (ignore.Count > 0);

                // Build the transitive closure of parent-child relationships
                foreach (var p in properties.Values)
                    p.TransitiveChildren.Clear();
                foreach (var p in properties.Values.Where(p => p.InheritsFrom != null))
                {
                    p.ImmediateParent = properties[p.InheritsFrom];
                    p.ImmediateParent.TransitiveChildren.Add(p);
                }
                // Keep adding children's children until no further changes (quite a brute-force algorithm... potential bottleneck)
                bool added;
                do
                {
                    added = false;
                    foreach (var p in properties.Values)
                        foreach (var c1 in p.TransitiveChildren.ToArray())
                            foreach (var c2 in c1.TransitiveChildren)
                                if (!p.TransitiveChildren.Contains(c2))
                                {
                                    p.TransitiveChildren.Add(c2);
                                    added = true;
                                }
                } while (added);

                // If there are no self-referencing properties then we're all done
                var looped = properties.Values.Where(p => p.TransitiveChildren.Contains(p)).ToArray();
                if (looped.Length == 0)
                    break;
                // Otherwise remove one circularly-referencing property and try again.
                warnings.Add(WdUtil.Tr.Error.DataDir_Skip_InhCircular.Fmt("<?>" /*origFilenames[looped[0]] broken... pls fix me*/));
                properties.Remove(looped[0].PropertyId.ToString());
            }

            // Compute the distance to nearest "root" in order to ensure that by the time an immediate parent is inherited from, the parent's inheritance has already been resolved
            foreach (var p in properties.Values)
                p.Depth = p.ImmediateParent == null ? 0 : -1;
            while (properties.Values.Any(p => p.Depth == -1))
            {
                foreach (var p in properties.Values.Where(p => p.Depth == -1 && p.ImmediateParent.Depth != -1))
                    p.Depth = p.ImmediateParent.Depth + 1;
            }

            // Finally, perform the actual inheritance
            foreach (var p in properties.Values.Where(p => p.ImmediateParent != null).OrderBy(df => df.Depth))
            {
                foreach (var kvp in p.ImmediateParent.Entries)
                {
                    var parent = kvp.Value;
                    var child = p.Entries[kvp.Key];
                    // If the child has an unversioned entry, do not inherit any of the parent entries
                    if (child.Any(e => e.GameVersionId == null))
                        continue;
                    // Otherwise mesh the two together, with the child taking precedence only where the same game version ID is specified in both
                    foreach (var pe in parent)
                        if (!child.Any(ce => ce.GameVersionId == pe.GameVersionId))
                            child.Add(pe);
                }
            }

            // Store the results in a publicly accessible, read-only format
            var result = new List<resolvedExtraProperty>();
            foreach (var p in properties.Values)
            {
                result.Add(new resolvedExtraProperty(p.PropertyId, p.Descriptions, p.Entries.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.OrderBy(e => e.GameVersionId ?? -1).ToList()
                )));
            }
            return result;
        }

        private static WotContext resolve(GameInstallation installation, GameVersionConfig versionConfig, Dictionary<string, List<TankEntry>> builtins, List<resolvedExtraProperty> extras, List<string> warnings, string defaultAuthor)
        {
            var gameVerId = installation.GameVersionId.Value; // cannot be null; the caller checks this
            var context = new WotContext(installation, versionConfig, warnings, defaultAuthor);
            var extrasResolved = new HashSet<ExtraPropertyId>();

            foreach (var builtin in builtins)
            {
                var applicable = builtin.Value.Where(e => e.GameVersionId == null || e.GameVersionId <= gameVerId).ToList();
                if (applicable.Count == 0)
                    continue; // this tank does not exist in the selected game version

                // Resolve the builtin data
                var country = Ut.OnExceptionDefault(() => applicable.Where(e => e.Country != null).Last().Country, null);
                var tier = Ut.OnExceptionDefault(() => applicable.Where(e => e.Tier != null).Last().Tier, null);
                var class_ = Ut.OnExceptionDefault(() => applicable.Where(e => e.Class != null).Last().Class, null);
                var category = Ut.OnExceptionDefault(() => applicable.Where(e => e.Category != null).Last().Category, null);
                if (country == null || tier == null || class_ == null || category == null)
                {
                    warnings.Add("Built-in data for tank {0} is unresolvable: one of the basic properties (country, tier, class or availability) is completely missing after all the inheritance has been resolved.".Fmt(builtin.Key));
                    continue; // this tank is unresolvable for the specified game version
                }

                // Resolve the extra data
                var props = new Dictionary<ExtraPropertyId, string>();
                foreach (var extra in extras)
                {
                    if (!extra.Entries.ContainsKey(builtin.Key))
                        continue;
                    var applicable2 = extra.Entries[builtin.Key].Where(e => e.GameVersionId == null || e.GameVersionId <= gameVerId);
                    if (!applicable2.Any())
                        continue;
                    props.Add(
                        extra.PropertyId,
                        applicable2.Last().Value
                    );
                    extrasResolved.Add(extra.PropertyId);
                }
                context.Tanks.Add(new WotTank(builtin.Key, country.Value, tier.Value, class_.Value, category.Value, props, context));
            }

            foreach (var prop in extrasResolved)
                context.ExtraProperties.Add(new ExtraPropertyInfo(prop, extras.First(extra => extra.PropertyId == prop).Descriptions));

            context.Freeze();
            return context;
        }

        #region Private classes

        private sealed class TankEntry
        {
            public string TankId { get; private set; }
            public Country? Country { get; private set; }
            public int? Tier { get; private set; }
            public Class? Class { get; private set; }
            public Category? Category { get; private set; }

            public int? GameVersionId { get; private set; }
            public bool Delete { get; private set; }

            public TankEntry(string tankId, Country? country, int? tier, Class? class_, Category? category, int? gameVersionId, bool delete)
            {
                TankId = tankId;
                Country = country;
                Tier = tier;
                Class = class_;
                Category = category;
                GameVersionId = gameVersionId;
                Delete = delete;
            }

            public override string ToString()
            {
                return "Tank: " + TankId + (GameVersionId == null ? "" : " #{0}".Fmt(GameVersionId)) + (Delete ? " del" : "");
            }
        }

        /// <summary>Represents a single value of an "extra" property.</summary>
        private sealed class ExtraEntry
        {
            public string TankId { get; private set; }
            public string Value { get; private set; }
            public int? GameVersionId { get; private set; }

            public ExtraEntry(string tankId, string propertyValue, int? gameVersionId)
            {
                TankId = tankId;
                Value = propertyValue;
                GameVersionId = gameVersionId;
            }

            public override string ToString()
            {
                return "{0} = {1}".Fmt(TankId, Value) + (GameVersionId == null ? "" : " (#{0})".Fmt(GameVersionId));
            }
        }

        private sealed class unresolvedBuiltIn
        {
            public int FileVersion;
            public List<TankEntry> Entries;
            public override string ToString() { return "WotData-BuiltIn-{0}".Fmt(FileVersion); }
        }

        private sealed class unresolvedExtraFileCol
        {
            public ExtraPropertyId PropertyId;
            /// <summary>Optional; empty if it wasn't specified.</summary>
            public Dictionary<string, string> Descriptions;
            /// <summary>The file version of the file this property comes from. Data read from the game itself has this set to 0.</summary>
            public int FileVersion;
            /// <summary>Optional; null if it wasn't specified.</summary>
            public string InheritsFrom;

            public List<ExtraEntry> Entries;

            public override string ToString()
            {
                return PropertyId.ToString() + " (v{0})".Fmt(FileVersion) + (InheritsFrom == null ? "" : " (inh {0})".Fmt(InheritsFrom));
            }
        }

        private sealed class unresolvedExtraProperty
        {
            public ExtraPropertyId PropertyId;
            /// <summary>Optional; empty if it wasn't specified.</summary>
            public Dictionary<string, string> Descriptions;
            /// <summary>Optional; null if it wasn't specified.</summary>
            public string InheritsFrom;

            public AutoDictionary<string, HashSet<ExtraEntry>> Entries;

            public override string ToString()
            {
                return PropertyId.ToString() + (InheritsFrom == null ? "" : " (inh {0})".Fmt(InheritsFrom));
            }

            public unresolvedExtraProperty ImmediateParent;
            public HashSet<unresolvedExtraProperty> TransitiveChildren = new HashSet<unresolvedExtraProperty>();
            public int Depth;
        }

        private sealed class resolvedExtraProperty
        {
            public ExtraPropertyId PropertyId { get; private set; }
            /// <summary>Optional; empty if it wasn't specified.</summary>
            public Dictionary<string, string> Descriptions { get; private set; }

            public Dictionary<string, List<ExtraEntry>> Entries { get; private set; }

            public resolvedExtraProperty(ExtraPropertyId propertyId, Dictionary<string, string> descriptions, Dictionary<string, List<ExtraEntry>> entries)
            {
                PropertyId = propertyId;
                Descriptions = descriptions;
                Entries = entries;
            }

            public override string ToString()
            {
                return "Property " + PropertyId;
            }
        }

        #endregion

    }
}