# Bincude
Tool for creating and unpacking .BIN files offered in [Escude](https://vndb.org/p403) visual novel games, specifically only those with the header ESC-ARC2.

### How are BIN files structured?
While the code also documents how a .BIN file is structured, here it is also the same information on a more accessible manner.

The file is divided into several parts:
  * **Magic signature** (8 bytes): this indicates the version of the file.
  * **Initial XOR seed** (4 bytes): the initial value used for the XOR operations.
  * **Number of files** (4 XOR'ed bytes): the number of files contained in the .BIN file.
  * **Length of file names** (4 XOR'ed bytes); the size of the array that contains the names of the files inside the .BIN file.
  * **Metadata of files** (*Number of files* bytes * 12 XOR'ed bytes): the region where the metadata of each file is stored.
    * **Name offset** (4 bytes): the relative offset (while only taking into consideration the index itself) of the file name
    * **Contents offset** (4 bytes): the offset of the actual file contents for said file.
    * **File size** (4 bytes): the size of the file contents in bytes.
  * **File names** (Length of file names bytes): the actual name of the files, separated by null bytes. If these files are inside a folder, the full path is included for each file.
  * **File contents** (Variable bytes): the actual contents of the files, always null terminated.

Besides the XOR protection, there are some files that are compressed, specifically all of the files that are not images. While not mandatory, Escude decides (probably because it also allows them to hide the actual bytes) that the **file contents** are compressed using LZW. Surprisingly enough, they seem to use a format that starts with a *0\acp* header for all of the files that are like this, and, all of the bytes have to be read in big endian.

While nothing can be confirmed, this seems to derive from a really old format of the 80's called .ARC, which used similar compression techniques and since the Motorola 8086 (which is big endian) was the defacto option for most machines, the format was designed that way.

#### In this program, this procedure is not done when recreating .BIN files since it is completely unecessary.
