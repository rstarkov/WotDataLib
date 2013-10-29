using System;
using System.Xml.Linq;
using RT.Util;
using RT.Util.Xml;

namespace WotDataLib
{
    /// <summary>Identifies an "extra" property. Suitable for use as dictionary keys.</summary>
    public sealed class ExtraPropertyId : IEquatable<ExtraPropertyId>, IXmlClassifyProcess2
    {
        /// <summary>FileId identifies a data file, along with the <see cref="Author"/>. Not null.</summary>
        public string FileId { get; private set; }
        /// <summary>
        ///     Optional; null if not specified. A file may contain multiple properties, distinguished by ColumnId. For files
        ///     with a single property, the ColumnId is optional.</summary>
        public string ColumnId { get; private set; }
        /// <summary>Identifies the author of a data file. Not null.</summary>
        public string Author { get; private set; }

        public static readonly ExtraPropertyId TierArabic = new ExtraPropertyId { FileId = "Tier (Arabic)", Author = "(built-in)" };
        public static readonly ExtraPropertyId TierRoman = new ExtraPropertyId { FileId = "Tier (Roman)", Author = "(built-in)" };

        public ExtraPropertyId(string fileId, string columnId, string author)
        {
            if (fileId == null || author == null)
                throw new ArgumentNullException();
            FileId = fileId;
            ColumnId = columnId;
            Author = author;
        }

        private ExtraPropertyId() { } // for XmlClassify

        public override bool Equals(object obj) { return Equals(obj as ExtraPropertyId); }

        public bool Equals(ExtraPropertyId other)
        {
            return other != null && FileId == other.FileId && ColumnId == other.ColumnId && Author == other.Author;
        }

        [XmlIgnore]
        private int _hash = 0;

        public override int GetHashCode()
        {
            if (_hash == 0)
            {
                _hash = unchecked((FileId ?? "").GetHashCode() + (ColumnId ?? "").GetHashCode() * 1049 + (Author ?? "").GetHashCode() * 5507);
                if (_hash == 0)
                    _hash = 1;
            }
            return _hash;
        }

        public override string ToString() { return FileId + "/" + (ColumnId == null ? "" : (ColumnId + "/")) + Author; }

        public static bool operator ==(ExtraPropertyId a, ExtraPropertyId b)
        {
            if ((object) a == null && (object) b == null)
                return true;
            else if ((object) a == null || (object) b == null)
                return false;
            else
                return a.Equals(b);
        }

        public static bool operator !=(ExtraPropertyId a, ExtraPropertyId b)
        {
            return !(a == b);
        }

        void IXmlClassifyProcess2.AfterXmlDeclassify(XElement xml)
        {
            var name = xml.Element("Name").NullOr(v => v.Value);
            var author = xml.Element("Author").NullOr(v => v.Value);

            if (name == "NameShortWG" && author == "Romkyns")
            {
                FileId = "NameShort";
                Author = "Wargaming";
            }
            else if (name == "NameFullWG" && author == "Romkyns")
            {
                FileId = "NameFull";
                Author = "Wargaming";
            }
            else if (name == "NameImproved" && author == "Romkyns")
            {
                FileId = "NameNative";
                ColumnId = "Ru";
            }
            else if (name != null)
            {
                FileId = name;
                if (name == "NameNative")
                    ColumnId = "Ru";
                if (author == "(built-in)")
                    ColumnId = null;
            }
        }

        void IXmlClassifyProcess2.AfterXmlClassify(XElement xml) { }
        void IXmlClassifyProcess2.BeforeXmlClassify(XElement xml) { }
        void IXmlClassifyProcess2.BeforeXmlDeclassify(XElement xml) { }
    }
}
