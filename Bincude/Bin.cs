using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;

namespace Bincude
{
    internal class Bin
    {
        /// <summary>
        /// Function that returns the version of the .BIN file based on its signature.
        /// </summary>
        /// <param name="Signature"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        static int GetVersion(byte[] Signature)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding shiftJIS = Encoding.GetEncoding("shift-jis");

            switch(shiftJIS.GetString(Signature))
            {
                case "ESC-ARC1":
                    return 1;
                case "ESC-ARC2":
                    return 2;
                default:
                    throw new Exception("Unknown version signature: " + shiftJIS.GetString(Signature));
            }
        }

        /// <summary>
        /// Function that decompiles any .BIN files provided in the ListOfFiles parameter.
        /// The structure that is returned is a list of Helper.FileInfo objects, which contain the name and data of the decompiled files.
        /// For documentation purposes, the structure of a .BIN file is as follows:
        /// Signature (8 bytes): this indicates the version of the file.
        /// Initial seed (4 bytes): the initial value used for the XOR operation.
        /// Number of files (4 encrypted bytes): the number of files contained in the .BIN file.
        /// Length of file names (4 encrypted bytes): the size of the array that contains the names of the files.
        /// Metadata of files (Number of files * 12 encrypted bytes): the region where the metadata of each file is stored, which is the following:
        ///     - Name offset (4 bytes): the relative offset (while only taking into consideration the index itself) of the file name
        ///     - Contents offset (4 bytes): the offset of the actual file contents for said file.
        ///     - File size (4 bytes): the size of the file contents in bytes.
        /// File names (Length of file names bytes): the actual name of the files, separated by null bytes. If these files are inside a folder, the
        /// full path is included for each file.
        /// File contents (Variable bytes): the actual contents of the files, always null terminated.
        /// </summary>
        public static List<Helper.FileInfo> Decompile(Helper.FileInfo OriginalFile)
        {
            List<Helper.FileInfo> DecompiledFiles = new List<Helper.FileInfo>();

            //First we obtain the version of the file
            byte[] Signature = new byte[8];
            Buffer.BlockCopy(OriginalFile.Data, 0, Signature, 0, 8);
            int Version = GetVersion(Signature);

            //Initially it was expected to support both versions, but seeing how old games are that use version 1, that got dropped
            if (Version == 1)
            {
                throw new Exception("Version 1 .BIN files are not supported.");
            }

            //Next, the initial seed is obtained for all XOR operations
            uint XORSeed = BitConverter.ToUInt32(OriginalFile.Data, 0x08);

            //Now we have the number of files contained in the .BIN file
            uint NumberOfFiles = BitConverter.ToUInt32(OriginalFile.Data, 0x0C) ^ NextKey(ref XORSeed);

            //After that, we have the length of the array that contains the names of the files
            uint FileNamesLength = BitConverter.ToUInt32(OriginalFile.Data, 0x10) ^ NextKey(ref XORSeed);

            //As stated in the summary above, the metadata of each file is stored in a region that is 12 bytes per file
            byte[] FilesMetadata = new byte[NumberOfFiles * 12];
            Buffer.BlockCopy(OriginalFile.Data, 0x14, FilesMetadata, 0, FilesMetadata.Length);

            //Decrypt the metadata of the files
            Decrypt(ref FilesMetadata, ref XORSeed);

            //Obtain the array for the names of the files
            byte[] FileNames = new byte[FileNamesLength];
            Buffer.BlockCopy(OriginalFile.Data, 0x14 + FilesMetadata.Length, FileNames, 0, FileNames.Length);

            int CurrentOffset = 0;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding shiftJIS = Encoding.GetEncoding("shift-jis");

            for (uint CurrentFile = 0; CurrentFile < NumberOfFiles; CurrentFile++)
            {
                int FileNameOffset = BitConverter.ToInt32(FilesMetadata, CurrentOffset);
                byte[] FileNameArray = new byte[FileNames.Length - FileNameOffset];
                Buffer.BlockCopy(FileNames, FileNameOffset, FileNameArray, 0, FileNameArray.Length);

                //All file names are separated with null bytes, so to avoid issues with the last element in the
                //array, we just check when the first null byte comes up within the obtained array based on the
                //file name offset
                int NullByteIndex = Array.IndexOf<byte>(FileNameArray, 0x00);

                string FileName;
                if (NullByteIndex >= 0)
                {
                    // Convert only bytes before the null terminator
                    FileName = shiftJIS.GetString(FileNameArray, 0, NullByteIndex);
                }
                else
                {
                    // No null terminator found, convert the entire array
                    FileName = shiftJIS.GetString(FileNameArray);
                }

                int FileContentsOffset = BitConverter.ToInt32(FilesMetadata, CurrentOffset + 4);
                int FileContentsSize = BitConverter.ToInt32(FilesMetadata, CurrentOffset + 8);
                byte[] FileContents = new byte[FileContentsSize];
                Buffer.BlockCopy(OriginalFile.Data, FileContentsOffset, FileContents, 0, FileContentsSize);

                DecompiledFiles.Add(
                    new Helper.FileInfo
                    {
                        Name = FileName,
                        Data = UnpackACP(FileContents) //Check to see if the file is a compressed ACP file and act accordingly
                    });

                CurrentOffset += 12; //Move to the next file's metadata
            }

            return DecompiledFiles;
        }

        /// <summary>
        /// Function that compiles the given files inside the ListOfFiles parameter into a .BIN file of the selected version.
        /// The byte array will vary depending on the version selected, which is already explained in the Decompile function.
        /// Something worth noting, is that the LZW compression offered in most files officially will not be reproduced here,
        /// as it is not necessary and will hamper any actual debugging in case of issues in the future.
        /// </summary>
        public static byte[] Compile(List<Helper.FileInfo> UncompressedFiles, string SelectedVersion)
        {
            List<byte> FinalFile = new List<byte>();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding shiftJIS = Encoding.GetEncoding("shift-jis");

            //First we obtain the version of the file
            byte[] Signature = new byte[8];
            Signature = shiftJIS.GetBytes(SelectedVersion);
            int Version = GetVersion(Signature);

            FinalFile.AddRange(Signature);

            //Initial seed for the XOR operation, we leave it at blank to avoid encrypting the file
            uint XORSeed = 0x00000000;
            FinalFile.AddRange(BitConverter.GetBytes(XORSeed));

            //The number of files stored in the bin file
            uint NumberOfFiles = (uint)UncompressedFiles.Count ^ NextKey(ref XORSeed);
            FinalFile.AddRange(BitConverter.GetBytes(NumberOfFiles));

            //Obtain the metadata of each file and creating the array of file names
            List<byte> FileNames = new List<byte>();
            uint[] FileNamesOffsets = new uint[UncompressedFiles.Count];
            uint[] FileSizes = new uint[UncompressedFiles.Count];
            for (int CurrentFile = 0; CurrentFile < UncompressedFiles.Count; CurrentFile++)
            {
                FileNamesOffsets[CurrentFile] = (uint)FileNames.Count; //Offset of the file name relative to the start of the file names array
                FileSizes[CurrentFile] = (uint)UncompressedFiles[CurrentFile].Data.Length + 1; //Size of the file contents, with the null terminator
                FileNames.AddRange(shiftJIS.GetBytes(UncompressedFiles[CurrentFile].Name));
                FileNames.Add(0x00);
            }
            
            uint[] FileOffsets = new uint[UncompressedFiles.Count];
            for (int CurrentFile = 0; CurrentFile < UncompressedFiles.Count; CurrentFile++)
            {
                if (CurrentFile == 0)
                {
                    //The first file will always be located after the file names array
                    FileOffsets[CurrentFile] = (uint)(0x14 + (UncompressedFiles.Count * 12) + FileNames.Count);
                }
                else
                {
                    //The rest of the files will be located after the previous file, taking into consideration its size
                    FileOffsets[CurrentFile] = FileOffsets[CurrentFile - 1] + FileSizes[CurrentFile - 1];
                }
            }

            //The length of the array that contains the names of the files
            uint FileNamesLength = (uint)FileNames.Count ^ NextKey(ref XORSeed);
            FinalFile.AddRange(BitConverter.GetBytes(FileNamesLength));

            //XOR'ing and adding to the final file the metadata of each file
            for (int CurrentFile = 0; CurrentFile < UncompressedFiles.Count; CurrentFile++)
            {
                FinalFile.AddRange(BitConverter.GetBytes(FileNamesOffsets[CurrentFile] ^ NextKey(ref XORSeed)));
                FinalFile.AddRange(BitConverter.GetBytes(FileOffsets[CurrentFile] ^ NextKey(ref XORSeed)));
                FinalFile.AddRange(BitConverter.GetBytes(FileSizes[CurrentFile] ^ NextKey(ref XORSeed)));
            }

            //Adding the names of the files to the final file
            FinalFile.AddRange(FileNames);

            //Adding the contents of each file to the final file, with a null terminator at the end
            for (int CurrentFile = 0; CurrentFile < UncompressedFiles.Count; CurrentFile++)
            {
                FinalFile.AddRange(UncompressedFiles[CurrentFile].Data);
                FinalFile.Add(0x00);
            }

            return FinalFile.ToArray();
        }

        /// <summary>
        /// Function that is used in the decompilation process to generate the next key for the XOR operation.
        /// </summary>
        static uint NextKey(ref uint Seed)
        {
            Seed ^= 0x65AC9365; //Harcoded value that adds diffusion into the XOR pattern
            Seed ^= (((Seed >> 1) ^ Seed) >> 3) 
                ^ (((Seed << 1) ^ Seed) << 3);
            return Seed;
        }

        /// <summary>
        /// Function that XORs the selected array of data given.
        /// </summary>
        static void Decrypt(ref byte[] Data, ref uint Seed)
        {
            int FullBlocks = Data.Length / 4;
            int Remainder = Data.Length % 4;

            for (int CurrentBlock = 0; CurrentBlock < FullBlocks; CurrentBlock++)
            {
                uint DecryptedBlock = BitConverter.ToUInt32(Data, CurrentBlock * 4);
                DecryptedBlock ^= NextKey(ref Seed);
                byte[] DecryptedArray = BitConverter.GetBytes(DecryptedBlock);
                Buffer.BlockCopy(DecryptedArray, 0, Data, CurrentBlock * 4, 4);
            }

            //XOR the remaining bytes
            for (int CurrentByte = 0; CurrentByte < Remainder; CurrentByte++)
            {
                byte keyByte = (byte)(NextKey(ref Seed) & 0xFF);
                Data[FullBlocks * 4 + CurrentByte] ^= keyByte;
            }
        }

        /// <summary>
        /// Some files are stored in a compressed format, which is the ACP format, a format that seems to be widely used.
        /// Something very important to take into consideration is that the entire format is read in big-endian mode.
        /// The structure that is used for ACP files is as follows:
        ///     - Magic signature (4 bytes): is always "acp\0".
        ///     - File size (4 bytes)
        ///     - File content (File size bytes)
        /// </summary>
        /// <param name="Data"></param>
        static byte[] UnpackACP(byte[] Data)
        {
            int CurrentOffset = 0;
            byte[] MagicSignature = { 0x61, 0x63, 0x70, 0x00 }; //acp\0
            byte[] FileSignature = new byte[MagicSignature.Length];
            Buffer.BlockCopy(Data, CurrentOffset, FileSignature, 0, FileSignature.Length);
            if (!FileSignature.SequenceEqual(MagicSignature))
            {
                return Data; //Not an ACP file, so we just return the original data
            }
            CurrentOffset += 4;

            byte[] FileSizeArray = new byte[4];
            Buffer.BlockCopy(Data, CurrentOffset, FileSizeArray, 0, FileSizeArray.Length);
            Array.Reverse(FileSizeArray); //The file size is stored in big-endian format, so we need to reverse it
            int FileSize = BitConverter.ToInt32(FileSizeArray, 0);
            CurrentOffset += 4;

            byte[] FileContent = new byte[Data.Length - CurrentOffset];
            Buffer.BlockCopy(Data, CurrentOffset, FileContent, 0, FileContent.Length);

            return Helper.LZW_Uncompresss(FileContent, FileSize); //Uncompress the file contents using LZW decompression
        }
    }
}
