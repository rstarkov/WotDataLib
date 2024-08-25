using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WotDataLib.Lingo;

namespace WotDataLib
{
    /// <summary>Some utilities internal to WotDataLib.</summary>
    static class WdUtil
    {
        /// <summary>Roman numerals for the tank tiers, hence only the values 1-10 are required.</summary>
        public static readonly string[] RomanNumerals = new[] { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI" };

        /// <summary>Provides a shorter way of accessing the active translation.</summary>
        public static Translation Tr { get { return WotData.Translation; } }

        /// <summary>
        ///     Enumerates the rows of a CSV file. Each row is represented by a tuple containing the line number and an array
        ///     of fields. Does not handle all valid CSV files: for example, multi-line field values are not supported.</summary>
        public static IEnumerable<Tuple<int, string[]>> ReadCsvLines(string filename)
        {
            int num = 0;
            foreach (var line in File.ReadLines(filename))
            {
                num++;
                var lineTrim = line.Trim();
                if (lineTrim == "" || lineTrim.StartsWith("#"))
                    continue;
                var fields = parseCsvLine(line);
                if (fields == null)
                    throw new WotDataUserError(WdUtil.Tr.Error.DataFile_CsvParse.Fmt(num));
                yield return Tuple.Create(num, fields);
            }
        }

        private static string[] parseCsvLine(string line)
        {
            var fields = Regex.Matches(line, @"(^|(?<=,)) *(?<quote>""?)(("""")?[^""]*?)*?\k<quote> *($|(?=,))").Cast<Match>().Select(m => m.Value).ToArray();
            if (line != string.Join(",", fields))
                return null;
            return fields.Select(f => f.Contains('"') ? Regex.Replace(f, @"^ *""(.*)"" *$", "$1").Replace(@"""""", @"""") : f.Trim()).ToArray();
        }

    }
}
