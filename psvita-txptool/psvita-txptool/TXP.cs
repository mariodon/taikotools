using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using static TaikoCompression.TaikoCompression;

namespace psvita_txptool
{
    class Txp
    {
        private const int Magic = 0x50585456; // 'VTXP'

        public int Version = 0x00010000; // ?
        public int NumberOfTextures;
        public uint HashSectionOffset;
        public byte[] Padding; // 16 bytes?

        private readonly List<TxpEntry> _entries;

        public Txp()
        {
            _entries = new List<TxpEntry>();
            Padding = new byte[16];
        }

        private int CalculatePadding(int max, int cur)
        {
            int padding = max - cur % max;

            if (padding == max)
                padding = 0;

            return padding;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(new byte[0x20]); // Write 0x20 bytes of blank data so we can come back to it later

            // Make sure all sections have a unique IndexA
            // If not, give them one
            _entries.Sort((x, y) => x.FileIndexA.CompareTo(y.FileIndexA));
            var fileIndexes = _entries.Select(x => x.FileIndexA);
            int nextFileIndex = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if(_entries[i].FileIndexA == -1)
                {
                    while(fileIndexes.Contains(nextFileIndex))
                    {
                        nextFileIndex++;
                    }

                    _entries[i].FileIndexA = nextFileIndex;
                }
            }

            // Do the same as above, but for FileIndexB
            _entries.Sort((x, y) => x.FileIndexB.CompareTo(y.FileIndexB));
            fileIndexes = _entries.Select(x => x.FileIndexB);
            nextFileIndex = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].FileIndexB == -1)
                {
                    while (fileIndexes.Contains(nextFileIndex))
                    {
                        nextFileIndex++;
                    }

                    _entries[i].FileIndexB = nextFileIndex;
                }
            }

            // File info
            _entries.Sort((x, y) => x.FileIndexA.CompareTo(y.FileIndexA));
            foreach (var entry in _entries)
            {
                entry.Write(writer);
            }

            // Filenames
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var curOffset = writer.BaseStream.Position;

                // Fix filename offset in section 1
                writer.BaseStream.Seek(0x20*(i+1), SeekOrigin.Begin);
                writer.Write((uint) curOffset);
                writer.BaseStream.Seek(curOffset, SeekOrigin.Begin);

                writer.Write(Encoding.GetEncoding(932).GetBytes(entry.InternalFilePath));
                writer.Write((byte)0); // Null terminated
            }

            writer.Write(new byte[CalculatePadding(4, (int)writer.BaseStream.Length)]); // Pad the filename section to the nearest 4th byte

            // Filename hashes
            _entries.Sort((x, y) => x.FileIndexB.CompareTo(y.FileIndexB));
            HashSectionOffset = (uint)writer.BaseStream.Length;
            foreach (var entry in _entries)
            {
                writer.Write(entry.FilenameHash);
                writer.Write(entry.FileIndexA);
            }

            writer.Write(new byte[CalculatePadding(0x80, (int)writer.BaseStream.Length)]); // Pad the filename section to the nearest 0x80th byte

            // Image data
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var curOffset = writer.BaseStream.Position;

                // Fix texture data offset in section 1
                writer.BaseStream.Seek(0x20 * (i+1) + 0x0c, SeekOrigin.Begin);
                writer.Write((int)curOffset);
                writer.BaseStream.Seek(curOffset, SeekOrigin.Begin);

                writer.Write(entry.TextureRawData);
            }

            NumberOfTextures = (ushort) _entries.Count;

            writer.BaseStream.Seek(0, SeekOrigin.Begin);
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(NumberOfTextures);
            writer.Write(HashSectionOffset);
            writer.Write(Padding);
        }

        public static Txp Create(string inputPath, string outputPath)
        {
            if (!Directory.Exists(inputPath))
            {
                return null;
            }
            
            Txp file = new Txp();

            // Look for all xml definition files in the folder
            var files = Directory.GetFiles(inputPath, "*.xml", SearchOption.AllDirectories);
            foreach (var xmlFilename in files)
            {
                TxpEntry entry = TxpEntry.FromFile(xmlFilename, inputPath);
                file._entries.Add(entry);
            }

            using (BinaryWriter writer = new BinaryWriter(File.Open(outputPath, FileMode.Create)))
            using (MemoryStream txpData = new MemoryStream())
            using (BinaryWriter txpWriter = new BinaryWriter(txpData))
            {
                file.Write(txpWriter);


                uint expectedFilesize = (uint)txpData.Length;

                if (expectedFilesize > 0xffffff)
                {
                    writer.Write(0x00000019);
                    writer.Write(expectedFilesize);
                }
                else
                {
                    writer.Write((expectedFilesize << 8) | 0x19);
                }

                txpData.Seek(0, SeekOrigin.Begin);

                var d = Compress(txpData.ToArray());
                writer.Write(d);
            }

            Console.WriteLine("Finished! Saved to {0}", outputPath);

            return file;
        }

        public static Txp Read(string inputPath, string outputPath, bool isRawExtract = false)
        {
            if (!File.Exists(inputPath))
            {
                return null;
            }

            using (BinaryReader reader = new BinaryReader(File.Open(inputPath, FileMode.Open)))
            {
                var expectedFilesize = reader.ReadUInt32();

                if (expectedFilesize == 0x19) // Blank
                {
                    expectedFilesize = reader.ReadUInt32();
                }
                else
                {
                    expectedFilesize = (expectedFilesize & 0xffffff00) >> 8;
                }

                var data = Decompress(reader.ReadBytes((int)reader.BaseStream.Length - 4));
                if (data.Length != expectedFilesize)
                {
                    Console.WriteLine("Filesize didn't match expected output filesize. Maybe bad decompression? ({0:x8} != {1:x8})", data.Length, expectedFilesize);
                }

                return Read(data, inputPath, outputPath, isRawExtract);
            }
        }

        public static Txp Read(byte[] data, string inputPath, string outputPath, bool isRawExtract = false)
        {
            Txp file = new Txp();

            using(MemoryStream dataStream = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(dataStream))
            {
                if (reader.ReadUInt32() != Magic)
                {
                    Console.WriteLine("ERROR: Not a valid TXP file.");
                }

                file.Version = reader.ReadInt32();
                file.NumberOfTextures = reader.ReadInt32();
                file.HashSectionOffset = reader.ReadUInt32();

                // File info
                for (int i = 0; i < file.NumberOfTextures; i++)
                {
                    reader.BaseStream.Seek(0x20 * (i + 1), SeekOrigin.Begin);

                    TxpEntry entry = TxpEntry.Read(reader);
                    entry.FileIndexA = file._entries.Count;
                    file._entries.Add(entry);
                }

                // Filename hashes
                reader.BaseStream.Seek(file.HashSectionOffset, SeekOrigin.Begin);
                for (int i = 0; i < file.NumberOfTextures; i++)
                {
                    file._entries[i].FilenameHash = reader.ReadUInt32();

                    int idx = reader.ReadInt32();
                    if (idx < file._entries.Count)
                    {
                        file._entries[idx].FileIndexB = i;
                    }
                    else
                    {
                        Console.WriteLine("ERROR(?): Found hash entry without a matching file entry");
                    }
                }

                // Image data
                for (int i = 0; i < file.NumberOfTextures; i++)
                {
                    // Palette data
                    if (file._entries[i].PaletteOffset != 0)
                    {
                        reader.BaseStream.Seek(file._entries[i].PaletteOffset, SeekOrigin.Begin);
                        file._entries[i].PaletteData = new uint[0x100];

                        for (int x = 0; x < 0x100; x++)
                        {
                            file._entries[i].PaletteData[x] = reader.ReadUInt32();
                        }
                    }

                    reader.BaseStream.Seek(file._entries[i].TextureOffset, SeekOrigin.Begin);
                    file._entries[i].TextureRawData = new byte[file._entries[i].TextureSize];
                    reader.Read(file._entries[i].TextureRawData, 0, (int)file._entries[i].TextureSize);
                }
                
                for (int i = 0; i < file.NumberOfTextures; i++)
                {
                    Console.WriteLine("Converting {0}...", file._entries[i].InternalFilePath);
                    file._entries[i].ToFile(outputPath, isRawExtract);
                }
            }

            return file;
        }
    }
}
