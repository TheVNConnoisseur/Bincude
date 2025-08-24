using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bincude
{
    internal static class Helper
    {
        private static byte[] CurrentData;
        private static int ByteOffset;
        private static int BitBuffer;
        private static int BitsInBuffer;

        public class FileInfo
        {
            public required string Name { get; set; }
            public required byte[] Data { get; set; }
        }

        private static void InitializeBitReader(byte[] Data)
        {
            CurrentData = Data;
            ByteOffset = 0;
            BitBuffer = 0;
            BitsInBuffer = 0;
        }
        
        public static byte[] LZW_Uncompresss(byte[] CompressedData, int FinalSize)
        {
            InitializeBitReader(CompressedData);
            byte[] UncompressedData = new byte[FinalSize];

            int Destination = 0;
            var Dictionary = new int[0x8900];
            int TokenWidth = 9;
            int DictionaryOffset = 0;
            while (Destination < UncompressedData.Length)
            {
                int Token = ReadBits(TokenWidth);
                if (-1 == Token)
                    throw new EndOfStreamException("Invalid LZW file stream.");
                else if (0x100 == Token) //End of input
                    break;
                else if (0x101 == Token) //Increase token width
                {
                    ++TokenWidth;
                    if (TokenWidth > 24)
                        throw new Exception("Invalid LZW file stream.");
                }
                else if (0x102 == Token) //Reset dictionary
                {
                    TokenWidth = 9;
                    DictionaryOffset = 0;
                }
                else
                {
                    if (DictionaryOffset >= Dictionary.Length)
                        throw new Exception("Invalid LZW file stream.");
                    Dictionary[DictionaryOffset++] = Destination;
                    if (Token < 0x100)
                    {
                        UncompressedData[Destination++] = (byte)Token;
                    }
                    else
                    {
                        Token -= 0x103;
                        if (Token >= DictionaryOffset)
                            throw new Exception("Invalid LZW file stream.");
                        int Source = Dictionary[Token];
                        int Count = Math.Min(UncompressedData.Length - Destination, Dictionary[Token + 1] - Source + 1);
                        if (Count < 0)
                            throw new Exception("Invalid LZW file stream.");
                        CopyOverlapped(UncompressedData, Source, Destination, Count);
                        Destination += Count;
                    }
                }
            }

            return UncompressedData;
        }

        private static void CopyOverlapped(byte[] Data, int Source, int Destination, int Count)
        {
            if (Destination > Source)
            {
                while (Count > 0)
                {
                    int Length = Math.Min(Destination - Source, Count);
                    Buffer.BlockCopy(Data, Source, Data, Destination, Length);
                    Destination += Length;
                    Count -= Length;
                }
            }
            else
            {
                Buffer.BlockCopy(Data, Source, Data, Destination, Count);
            }
        }

        private static int ReadBits(int count)
        {
            while (BitsInBuffer < count)
            {
                if (ByteOffset >= CurrentData.Length)
                    return -1;

                BitBuffer = (BitBuffer << 8) | CurrentData[ByteOffset++];
                BitsInBuffer += 8;
            }

            int mask = (1 << count) - 1;
            BitsInBuffer -= count;

            return (BitBuffer >> BitsInBuffer) & mask;
        }
    }
}
