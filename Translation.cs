using RT.Util.Lingo;

namespace WotDataLib.Lingo
{
    enum WdTranslationGroup
    {
        [LingoGroup("WoT Data: Errors", "Error messages in the WoT Data library.")]
        WotDataErrors
    }

    [LingoStringClass]
    public sealed class Translation : TranslationBase
    {
        public Translation() : base(Language.EnglishUK) { }

        public ErrorTranslation Error = new ErrorTranslation();
    }

    [LingoStringClass, LingoInGroup(WdTranslationGroup.WotDataErrors)]
    public sealed class ErrorTranslation
    {
        public TrString DataFile_UnrecognizedCountry = "Unrecognized country: \"{0}\". Allowed values: {1}.";
        public TrString DataFile_UnrecognizedClass = "Unrecognized class: \"{0}\". Allowed values: {1}.";
        public TrString DataFile_UnrecognizedCategory = "Unrecognized availability: \"{0}\". Allowed values: {1}.";
        public TrString DataFile_TankTierValue = "The tier field is not a whole number or outside of the 1..10 range: \"{0}\"";
        public TrStringNum DataFile_TooFewFields = new TrStringNum("Expected at least 1 field.", "Expected at least {0} fields.");
        public TrString DataFile_EmptyFile = "Expected at least one line.";
        public TrString DataFile_TooFewFieldsFirstLine = "Expected at least two columns in the first row.";
        public TrString DataFile_ExpectedSignature = "Expected \"{0}\" in the first column of the first row.";
        public TrString DataFile_ExpectedV2 = "The second column of the first row must be \"2\" (format version)";
        [LingoNotes("The string \"{0}\" is replaced with the line number where the error occurred. \"{1}\" is replaced with the error message.")]
        public TrString DataFile_LineNum = "Line {0}: {1}";
        public TrString DataFile_CsvParse = "Couldn't parse line {0}.";

        public TrString DataDir_NoFilesAvailable = "Could not load any game version data files and/or any built-in property data files.";
        public TrString DataDir_Skip_WrongParts = "Skipped \"{0}\" because it has the wrong number of filename parts (expected: {1}, actual {2}).";
        [LingoNotes("The DataFile_* errors are interpolated into this string in place of \"{1}\".")]
        public TrString DataDir_Skip_FileError = "Skipped \"{0}\" because the file could not be parsed: {1}";
        public TrString DataDir_Skip_GameVersion = "Skipped \"{0}\" because it has an unparseable game version part in the filename: \"{1}\".";
        public TrString DataDir_Skip_FileVersion = "Skipped \"{0}\" because it has an unparseable file version part in the filename, or the file version is less than 1: \"{1}\".";
        public TrString DataDir_Skip_Author = "Skipped \"{0}\" because it has an empty author part in the filename.";
        public TrString DataDir_Skip_PropName = "Skipped \"{0}\" because it has an empty property name part in the filename.";
        public TrString DataDir_Skip_Lang = "Skipped \"{0}\" because its language name part in the filename (\"{1}\") is not a valid language code, nor \"X\" for language-less files. Did you mean En, Ru, Zh, Es, Fr, De, Ja? Full list of ISO-639-1 codes is available on Wikipedia.";

        public TrString DataDir_Skip_InhNoProp = "Skipped \"{0}\" because there are no data files for the property \"{1}\" (from which it inherits values).";
        public TrString DataDir_Skip_InhNoLang = "Skipped \"{0}\" because no data files for the property \"{1}\" (from which it inherits values) are in language \"{2}\".";
        public TrString DataDir_Skip_InhNoAuth = "Skipped \"{0}\" because no data files for the property \"{1}\" (from which it inherits values) are by author \"{2}\".";
        public TrString DataDir_Skip_InhNoGameVer = "Skipped \"{0}\" because no data files for the property \"{1}\"/\"{2}\" (from which it inherits values) have game version \"{3}\" or below.";
        public TrString DataDir_Skip_InhCircular = "Skipped \"{0}\" due to a circular dependency.";
    }
}
