namespace iPhoneTrackerWin
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    public class MbdbReader
    {
        public static MBFileRecord[] ReadMBDB(string backupPath, bool dump, bool checks, out string log)
        {
            log = string.Empty;

            MBFileRecord[] files = null;
            var rec = new MBFileRecord();
            var signature = new byte[6];                     // buffer signature
            var buf = new byte[26];                          // buffer for .mbdx record
            var sb = new StringBuilder(40);           // stringbuilder for the Key
            var data = new byte[40];                         // buffer for the fixed part of .mbdb record

            var unixEpoch = new System.DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

            // open the index
            var mbdxPath = Path.Combine(backupPath, "Manifest.mbdx");

            if (File.Exists(mbdxPath) == false)
            {
                log = "Manifest.mbdx couldn't be found!";
                return null;
            }

            using (var mbdx = new FileStream(mbdxPath, FileMode.Open, FileAccess.Read))
            {
                // skip signature
                mbdx.Read(signature, 0, 6);

                // "mbdx\2\0"
                if (BitConverter.ToString(signature, 0) != "6D-62-64-78-02-00")
                {
                    log = "Bad .mbdx file";
                    return null;
                }

                // open the database
                var mbdbPath = Path.Combine(backupPath, "Manifest.mbdb");

                if (File.Exists(mbdxPath) == false)
                {
                    log = "Manifest.mbdb couldn't be found!";
                    return null;
                }

                using (var mbdb = new FileStream(mbdbPath, FileMode.Open, FileAccess.Read))
                {
                    // skip signature
                    mbdb.Read(signature, 0, 6);

                    // "mbdb\5\0"
                    if (BitConverter.ToString(signature, 0) != "6D-62-64-62-05-00")
                    {
                        log = "Bad .mbdb file";
                        return null;
                    }

                    // number of records in .mbdx
                    if (mbdx.Read(buf, 0, 4) != 4)
                    {
                        log = "Altered .mbdx file";
                        return null;
                    }

                    var records = BigEndianBitConverter.ToInt32(buf, 0);
                    files = new MBFileRecord[records];

                    // loop through the records
                    for (int i = 0; i < records; ++i)
                    {
                        // get the fixed size .mbdx record
                        if (mbdx.Read(buf, 0, 26) != 26)
                        {
                            break;
                        }

                        // convert key to text, it's the filename in the backup directory
                        // in previous versions of iTunes, it was the file part of .mddata/.mdinfo
                        sb.Clear();

                        for (var j = 0; j < 20; ++j)
                        {
                            var b = buf[j];
                            sb.Append(ToHexLow(b >> 4));
                            sb.Append(ToHexLow(b & 15));
                        }

                        rec.Key = sb.ToString();
                        rec.Offset = BigEndianBitConverter.ToInt32(buf, 20);
                        rec.Mode = BigEndianBitConverter.ToUInt16(buf, 24);

                        // read the record in the .mbdb
                        mbdb.Seek(6 + rec.Offset, SeekOrigin.Begin);

                        rec.Domain = GetS(mbdb);
                        rec.Path = GetS(mbdb);
                        rec.LinkTarget = GetS(mbdb);
                        rec.DataHash = GetD(mbdb);
                        rec.AlwaysNA = GetD(mbdb);

                        mbdb.Read(data, 0, 40);

                        rec.Data = ToHex(data, 2, 4, 4, 4, 4, 4, 4, 4, 8, 1, 1);

                        // rec.ModeBis = BigEndianBitConverter.ToUInt16(data, 0);
                        rec.AlwaysZero = BigEndianBitConverter.ToInt32(data, 2);
                        rec.Unknown = BigEndianBitConverter.ToInt32(data, 6);
                        rec.UserId = BigEndianBitConverter.ToInt32(data, 10);       // or maybe GroupId (don't care...)
                        rec.GroupId = BigEndianBitConverter.ToInt32(data, 14);      // or maybe UserId

                        rec.TimeA = unixEpoch.AddSeconds(BigEndianBitConverter.ToUInt32(data, 18));
                        rec.TimeB = unixEpoch.AddSeconds(BigEndianBitConverter.ToUInt32(data, 22));
                        rec.TimeC = unixEpoch.AddSeconds(BigEndianBitConverter.ToUInt32(data, 26));

                        rec.FileLength = BigEndianBitConverter.ToInt64(data, 30);

                        rec.Flag = data[38];
                        rec.PropertyCount = data[39];

                        rec.Properties = new Property[rec.PropertyCount];
                        for (int j = 0; j < rec.PropertyCount; ++j)
                        {
                            rec.Properties[j].Name = GetS(mbdb);
                            rec.Properties[j].Value = GetD(mbdb);
                        }

                        files[i] = rec;

                        // debug print
                        if (dump)
                        {
                            Console.WriteLine("record {0} (mbdb offset {1})", i, rec.Offset + 6);

                            Console.WriteLine("  key    {0}", rec.Key);
                            Console.WriteLine("  domain {0}", rec.Domain);
                            Console.WriteLine("  path   {0}", rec.Path);
                            if (rec.LinkTarget != "n/a")
                            {
                                Console.WriteLine("  target {0}", rec.LinkTarget);
                            }

                            if (rec.DataHash != "n/a")
                            {
                                Console.WriteLine("  hash   {0}", rec.DataHash);
                            }

                            if (rec.AlwaysNA != "n/a")
                            {
                                Console.WriteLine("  unk3   {0}", rec.AlwaysNA);
                            }

                            var l = "?";
                            switch ((rec.Mode & 0xF000) >> 12)
                            {
                                case 0xA:
                                    l = "link";
                                    break;
                                case 0x4:
                                    l = "dir";
                                    break;
                                case 0x8:
                                    l = "file";
                                    break;
                            }

                            Console.WriteLine("  mode   {1} ({0})", rec.Mode & 0xFFF, l);

                            Console.WriteLine("  time   {0}", rec.TimeA);

                            // length is unsignificant if link or dir
                            if ((rec.Mode & 0xF000) == 0x8000)
                            {
                                Console.WriteLine("  length {0}", rec.FileLength);
                            }

                            Console.WriteLine("  data   {0}", rec.Data);

                            for (var j = 0; j < rec.PropertyCount; ++j)
                            {
                                Console.WriteLine("  pn[{0}]  {1}", j, rec.Properties[j].Name);
                                Console.WriteLine("  pv[{0}]  {1}", j, rec.Properties[j].Value);
                            }
                        }

                        // some assertions...
                        if (checks)
                        {
                            // Debug.Assert(rec.Mode == rec.ModeBis);
                            Debug.Assert(rec.AlwaysZero == 0);
                            if (rec.LinkTarget != "n/a")
                            {
                                Debug.Assert((rec.Mode & 0xF000) == 0xA000);
                            }

                            if (rec.DataHash != "n/a")
                            {
                                Debug.Assert(rec.DataHash.Length == 40);
                            }

                            Debug.Assert(rec.AlwaysNA == "n/a");

                            if (rec.Domain.StartsWith("AppDomain-"))
                            {
                                Debug.Assert(rec.GroupId == 501 && rec.UserId == 501);
                            }

                            if (rec.FileLength != 0)
                            {
                                Debug.Assert((rec.Mode & 0xF000) == 0x8000);
                            }

                            if ((rec.Mode & 0xF000) == 0x8000)
                            {
                                Debug.Assert(rec.Flag != 0);
                            }

                            if ((rec.Mode & 0xF000) == 0xA000)
                            {
                                Debug.Assert(rec.Flag == 0 && rec.FileLength == 0);
                            }

                            if ((rec.Mode & 0xF000) == 0x4000)
                            {
                                Debug.Assert(rec.Flag == 0 && rec.FileLength == 0);
                            }
                        }
                    }
                }
            }

            return files;
        }

        private static string GetS(Stream fs)
        {
            var b0 = fs.ReadByte();
            var b1 = fs.ReadByte();

            if (b0 == 255 && b1 == 255)
            {
                return "n/a";
            }

            var length = (b0 * 256) + b1;

            var buf = new byte[length];
            fs.Read(buf, 0, length);

            // We need to do a "Unicode normalization form C" (see Unicode 4.0 TR#15)
            // since some applications don't like the canonical decomposition (NormalizationD)...

            // More information: http://msdn.microsoft.com/en-us/library/dd319093(VS.85).aspx
            // or http://msdn.microsoft.com/en-us/library/8eaxk1x2.aspx
            var s = Encoding.UTF8.GetString(buf, 0, length);

            return s.Normalize(NormalizationForm.FormC);
        }

        private static char ToHex(int value)
        {
            value &= 0xF;

            if (value >= 0 && value <= 9)
            {
                return (char)('0' + value);
            }
            else
            {
                return (char)('A' + (value - 10));
            }
        }

        private static char ToHexLow(int value)
        {
            value &= 0xF;

            if (value >= 0 && value <= 9)
            {
                return (char)('0' + value);
            }
            else
            {
                return (char)('a' + (value - 10));
            }
        }

        private static string ToHex(byte[] data, params int[] spaces)
        {
            var sb = new StringBuilder(data.Length * 3);

            var n = 0;
            var p = 0;

            for (var i = 0; i < data.Length; ++i)
            {
                if (n < spaces.Length && i == p + spaces[n])
                {
                    sb.Append(' ');
                    p += spaces[n];
                    n++;
                }

                sb.Append(ToHex(data[i] >> 4));
                sb.Append(ToHex(data[i] & 15));
            }

            return sb.ToString();
        }

        private static int FromHex(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return (int)(c - '0');
            }

            if (c >= 'A' && c <= 'F')
            {
                return (int)(c - 'A' + 10);
            }

            if (c >= 'a' && c <= 'f')
            {
                return (int)(c - 'a' + 10);
            }

            return 0;
        }

        private static string GetD(Stream fs)
        {
            var b0 = fs.ReadByte();
            var b1 = fs.ReadByte();

            if (b0 == 255 && b1 == 255)
            {
                return "n/a";
            }

            var length = (b0 * 256) + b1;

            var buf = new byte[length];
            fs.Read(buf, 0, length);

            // if we have only ASCII printable characters, we return the string
            int i;
            for (i = 0; i < length; ++i)
            {
                if (buf[i] < 32 || buf[i] >= 128)
                {
                    break;
                }
            }

            if (i == length)
            {
                return Encoding.ASCII.GetString(buf, 0, length);
            }

            // otherwise the hexadecimal dump
            var sb = new StringBuilder(length * 2);

            for (i = 0; i < length; ++i)
            {
                sb.Append(ToHex(buf[i] >> 4));
                sb.Append(ToHex(buf[i] & 15));
            }

            return sb.ToString();
        }
    }
}