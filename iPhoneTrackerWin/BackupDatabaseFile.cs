namespace iPhoneTrackerWin
{
    using System;
    using System.IO;
    using System.Runtime.Serialization.Plists;
    using System.Xml;

    public class BackupDatabaseFile : DatabaseFile
    {
        private const string DisplayNamePropertyName = "Display Name";
        private const string LastBackupDatePropertyName = "Last Backup Date";

        public BackupDatabaseFile(string file, string directory)
        {
            this.File = file;
            this.Directory = directory;

            if (IsBinaryPropertyListFile(this.InfoPropertyListPath))
            {
                var reader = new BinaryPlistReader();

                var properties = reader.ReadObject(this.InfoPropertyListPath);

                if (properties.Contains(DisplayNamePropertyName))
                {
                    this.DisplayName = properties[DisplayNamePropertyName].ToString();
                }

                if (properties.Contains(LastBackupDatePropertyName))
                {
                    this.LastBackupDate = (DateTime)properties[LastBackupDatePropertyName];
                }
            }
            else
            {
                var info = GetDisplayNameAndLastBackupDateFromPropertyList(this.InfoPropertyListPath);

                this.DisplayName = info.Item1;
                this.LastBackupDate = info.Item2;
            }
        }

        public string Directory { get; set; }

        public string InfoPropertyListPath
        {
            get { return Path.Combine(this.Directory, "Info.plist"); }
        }

        public string DisplayName { get; set; }

        public DateTime? LastBackupDate { get; set; }

        public override string ToString()
        {
            return this.LastBackupDate.HasValue
                ? string.Format("{0} ({1:g})", this.DisplayName, this.LastBackupDate.Value)
                : this.DisplayName;
        }

        private static bool IsBinaryPropertyListFile(string path)
        {
            if (System.IO.File.Exists(path) == false)
            {
                return false;
            }

            using (var stream = System.IO.File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                var bpli = reader.ReadInt32().ToBigEndianConditional();
                var version = reader.ReadInt32().ToBigEndianConditional();

                return bpli == BinaryPlistReader.HeaderMagicNumber && version == BinaryPlistReader.HeaderVersionNumber;
            }
        }

        private static Tuple<string, DateTime?> GetDisplayNameAndLastBackupDateFromPropertyList(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);

            var resultNodes = doc.SelectNodes("//string[preceding-sibling::key[1]='" + DisplayNamePropertyName + "']");

            var displayName = string.Empty;

            if (resultNodes.Count == 1)
            {
                displayName = ((XmlElement)resultNodes[0]).InnerText;
            }

            resultNodes = doc.SelectNodes("//date[preceding-sibling::key[1]='" + LastBackupDatePropertyName + "']");

            DateTime? lastBackupDate = null;

            if (resultNodes.Count == 1)
            {
                var dateString = ((XmlElement)resultNodes[0]).InnerText;
                lastBackupDate = XmlConvert.ToDateTime(dateString, XmlDateTimeSerializationMode.Local);
            }

            return new Tuple<string, DateTime?>(displayName, lastBackupDate);
        }
    }
}