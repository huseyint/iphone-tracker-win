namespace iPhoneTrackerWin
{
    using System;

    public struct MBFileRecord
    {
        // from .mbdx
        // filename if the directory
        public string Key;

        // offset of record in the .mbdb file
        public int Offset;

        // 8xxx=dir, 4xxx=file, Axxx=symlink
        public ushort Mode;

        // from .mbdb
        public string Domain;
        
        public string Path;

        public string LinkTarget;

        // SHA.1 for 'important' files
        public string DataHash;
        
        public string AlwaysNA;

        // the 40-byte block (some fields still need to be explained)
        public string Data;

        // same as .mbdx field
        // public ushort ModeBis;
        public int AlwaysZero;
        
        public int Unknown;
        
        public int UserId;

        public int GroupId;

        // aTime or bTime is the former ModificationTime
        public DateTime TimeA;
        
        public DateTime TimeB;
        
        public DateTime TimeC;

        // always 0 for link or directory
        public long FileLength;

        // 0 if special (link, directory), otherwise values unknown
        public byte Flag;
        
        public byte PropertyCount;

        public Property[] Properties;
    }
}