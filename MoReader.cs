using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WotDataLib
{
    /// <summary>Helper class for reading World of Tanks .mo files, which contain string key/value pairs.</summary>
    public static class MoReader
    {
        /// <summary>Reads an .mo file from the specified path.</summary>
        public static IDictionary<string, string> ReadFile(string filename)
        {
            using (var reader = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
                return readFile(reader);
        }

        private static IDictionary<string, string> readFile(BinaryReader reader)
        {
            if (reader.ReadUInt32() != 0x950412DE)
                throw new WotDataException("This file does not look like a valid .mo file");
            reader.BaseStream.Position = 8;
            int entries = reader.ReadInt32();
            int unknown1 = reader.ReadInt32();
            int unknown2 = reader.ReadInt32();
            int unknown3 = reader.ReadInt32();
            int unknown4 = reader.ReadInt32();
            var result = new Dictionary<string, string>();
            for (int i = 0; i < entries; i++)
            {
                var key = readString(reader, 28 + 8 * i);
                var value = readString(reader, 28 + 8 * (i + entries));
                result[key] = value;
            }
            return result;
        }

        private static string readString(BinaryReader reader, int offset)
        {
            reader.BaseStream.Position = offset;
            int count = reader.ReadInt32();
            reader.BaseStream.Position = reader.ReadInt32();
            return Encoding.UTF8.GetString(reader.ReadBytes(count));
        }
    }
}
