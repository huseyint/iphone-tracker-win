namespace iPhoneTrackerWin
{
    using System;

    public class BigEndianBitConverter
    {
        public static short ToInt16(byte[] value, int startIndex)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToInt16(ReverseBytes(value, startIndex, 2), 0);
            }
            else
            {
                return BitConverter.ToInt16(value, startIndex);
            }
        }

        public static ushort ToUInt16(byte[] value, int startIndex)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToUInt16(ReverseBytes(value, startIndex, 2), 0);
            }
            else
            {
                return BitConverter.ToUInt16(value, startIndex);
            }
        }

        public static int ToInt32(byte[] value, int startIndex)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToInt32(ReverseBytes(value, startIndex, 4), 0);
            }
            else
            {
                return BitConverter.ToInt32(value, startIndex);
            }
        }

        public static uint ToUInt32(byte[] value, int startIndex)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToUInt32(ReverseBytes(value, startIndex, 4), 0);
            }
            else
            {
                return BitConverter.ToUInt32(value, startIndex);
            }
        }

        public static long ToInt64(byte[] value, int startIndex)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToInt64(ReverseBytes(value, startIndex, 8), 0);
            }

            return BitConverter.ToInt64(value, startIndex);
        }

        private static byte[] ReverseBytes(byte[] inArray, int offset, int count)
        {
            int j = count;
            byte[] ret = new byte[count];

            for (int i = offset; i < offset + count; ++i)
            {
                ret[--j] = inArray[i];
            }

            return ret;
        }
    }
}