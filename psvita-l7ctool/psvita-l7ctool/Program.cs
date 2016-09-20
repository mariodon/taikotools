using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using static TaikoCompression.TaikoCompression;

namespace psvita_l7ctool
{
    class L7CAHeader
    {
        public uint magic = 0x4143374c; // L7CA
        public uint unk = 0x00010000; // Version? Must be 0x00010000
        public int archiveSize;
        public int metadataOffset;
        public int metadataSize;
        public uint unk2 = 0x00010000; // Chunk max size?
        public int filesystemEntries;
        public int folders;
        public int files;
        public int chunks;
        public int stringTableSize;
        public int unk4 = 5; // Number of sections??
    }

    class L7CAFilesystemEntry
    {
        public uint id;
        public uint hash; // Hash of what?
        public int folderOffset;
        public int filenameOffset;
        public long timestamp;
        public string filename;
    }

    class L7CAFileEntry
    {
        public int compressedFilesize;
        public int rawFilesize;
        public int chunkIdx;
        public int chunkCount;
        public int offset;
        public uint crc32;
    }

    class L7CAChunkEntry
    {
        public int chunkSize;
        public ushort unk = 0;
        public ushort chunkId;
    }

    class Program
    {
        static bool DebugDecompressionCode = false;
        const int MaxChunkSize = 0x10000;
        const int PaddingBoundary = 0x200;

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("usage:");
                Console.WriteLine("Extraction:");
                Console.WriteLine("\t{0} x input.l7z", AppDomain.CurrentDomain.FriendlyName);
                Console.WriteLine();
                Console.WriteLine("Creation:");
                Console.WriteLine("\t{0} c input_foldername output.l7z", AppDomain.CurrentDomain.FriendlyName);
                Console.WriteLine();
                Console.WriteLine("Decompress individual file:");
                Console.WriteLine("\t{0} d input.bin output.bin", AppDomain.CurrentDomain.FriendlyName);
                Console.WriteLine();
                Console.WriteLine("Compress individual file:");
                Console.WriteLine("\t{0} e input.bin output.bin", AppDomain.CurrentDomain.FriendlyName);
                Environment.Exit(1);
            }

            if (args[0] == "c")
            {
                PackL7CA(args[1], args[2]);
            }
            else if (args[0] == "x")
            {
                UnpackL7CA(args[1]);
            }
            else if (args[0] == "d")
            {
                string input = args[1];
                string output = output = input + ".out";

                if (args.Length >= 2)
                {
                    output = args[2];
                }

                byte[] data;
                using (BinaryReader reader = new BinaryReader(File.OpenRead(input)))
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

                    data = Decompress(reader.ReadBytes((int)reader.BaseStream.Length - 4));
                    if (data.Length != expectedFilesize)
                    {
                        Console.WriteLine("Filesize didn't match expected output filesize. Maybe bad decompression? ({0:x8} != {1:x8})", data.Length, expectedFilesize);
                    }
                }

                File.WriteAllBytes(output, data);
            }
            else if (args[0] == "e")
            {
                string input = args[1];
                string output = output = input + ".out";

                if (args.Length >= 2)
                {
                    output = args[2];
                }

                byte[] data = File.ReadAllBytes(input);
                using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(output)))
                {
                    int expectedFilesize = data.Length;

                    if (expectedFilesize > 0xffffff)
                    {
                        writer.Write(0x00000019);
                        writer.Write(expectedFilesize);
                    }
                    else
                    {
                        writer.Write((expectedFilesize << 8) | 0x19);
                    }

                    writer.Write(Compress(data));
                }
            }
        }

        static void PackL7CA(string inputFoldername, string outputFilename)
        {
            MemoryStream headerSection = new MemoryStream();
            MemoryStream filesystemSection = new MemoryStream();
            MemoryStream fileEntriesSection = new MemoryStream();
            MemoryStream chunksSection = new MemoryStream();
            MemoryStream stringSection = new MemoryStream();

            // Build folders and paths
            List<string> paths = new List<string>();
            List<string> fullpaths = new List<string>();
            foreach (var file in Directory.EnumerateFiles(inputFoldername, "*.*", SearchOption.AllDirectories))
            {
                var curpaths = new List<string>();

                var path = file.Replace('\\', '/');
                while (!String.IsNullOrWhiteSpace((path = Path.GetDirectoryName(path))))
                {
                    path = path.Replace('\\', '/');

                    if (!paths.Contains(path))
                        curpaths.Add(path);
                }

                curpaths.Reverse();

                fullpaths.AddRange(curpaths);
                fullpaths.Add(file);

                var filename = Path.GetFileName(file);
                if (!paths.Contains(filename))
                {
                    curpaths.Add(filename);
                }

                paths.AddRange(curpaths);
            }

            // Build string table
            Dictionary<string, int> stringTableMapping = new Dictionary<string, int>();
            using (BinaryWriter writer = new BinaryWriter(stringSection, Encoding.ASCII, true))
            {
                foreach (var path in paths)
                {
                    var temp = Encoding.ASCII.GetBytes(path);
                    int offset = (int)writer.BaseStream.Position;

                    writer.Write((byte)temp.Length);
                    writer.Write(temp);

                    stringTableMapping.Add(path, offset);
                }
            }

            // Build filesystem table entries
            List<L7CAFilesystemEntry> filesystemEntries = new List<L7CAFilesystemEntry>();
            List<string> folders = new List<string>();
            List<string> files = new List<string>();
            uint filesystemId = 0;
            foreach (var path in fullpaths)
            {
                L7CAFilesystemEntry entry = new L7CAFilesystemEntry();

                if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                {
                    entry.id = 0xffffffff;
                    entry.folderOffset = stringTableMapping[path.Replace("\\", "/")];
                    folders.Add(path);
                }
                else
                {
                    entry.id = filesystemId++;
                    entry.folderOffset = stringTableMapping[Path.GetDirectoryName(path).Replace("\\", "/")];
                    entry.filenameOffset = stringTableMapping[Path.GetFileName(path)];
                    files.Add(path);
                }

                entry.hash = Crc32.CalculateNamco(path.Replace("\\", "/"));
                entry.timestamp = new FileInfo(path).LastWriteTime.ToFileTime();
                entry.filename = path;

                filesystemEntries.Add(entry);
            }

            using (BinaryWriter writer = new BinaryWriter(filesystemSection, Encoding.ASCII, true))
            {
                foreach (var entry in filesystemEntries)
                {
                    writer.Write(entry.id);
                    writer.Write(entry.hash);
                    writer.Write(entry.folderOffset);
                    writer.Write(entry.filenameOffset);
                    writer.Write(entry.timestamp);
                }
            }

            using (BinaryWriter archiveWriter = new BinaryWriter(File.OpenWrite(outputFilename)))
            {
                // Write heading padding
                archiveWriter.Write(new byte[PaddingBoundary]);

                // Build file entries
                List<L7CAFileEntry> fileEntries = new List<L7CAFileEntry>();
                List<L7CAChunkEntry> chunkEntries = new List<L7CAChunkEntry>();
                foreach (var fs in filesystemEntries)
                {
                    if (fs.id == 0xffffffff)
                        continue;


                    Console.WriteLine("Reading {0}...", fs.filename);

                    L7CAFileEntry entry = new L7CAFileEntry();
                    entry.chunkIdx = chunkEntries.Count;

                    var data = File.ReadAllBytes(fs.filename);
                    ushort chunks = 0;
                    for (int size = data.Length; size > 0; size -= MaxChunkSize)
                    {
                        L7CAChunkEntry chunkEntry = new L7CAChunkEntry();
                        chunkEntry.chunkId = chunks++;
                        chunkEntry.chunkSize = size > MaxChunkSize ? MaxChunkSize : size;
                        chunkEntries.Add(chunkEntry);
                    }

                    entry.chunkCount = chunks;
                    entry.compressedFilesize = data.Length;
                    entry.rawFilesize = data.Length;
                    entry.crc32 = Crc32.Calculate(data);
                    entry.offset = (int)archiveWriter.BaseStream.Position;

                    // Write data to data table
                    archiveWriter.Write(data);

                    // Write padding
                    archiveWriter.Write(new byte[(int)(PaddingBoundary - ((archiveWriter.BaseStream.Length - PaddingBoundary) % PaddingBoundary))]);

                    fileEntries.Add(entry);
                }

                // Write file entries section data
                using (BinaryWriter writer = new BinaryWriter(fileEntriesSection, Encoding.ASCII, true))
                {
                    foreach (var entry in fileEntries)
                    {
                        writer.Write(entry.compressedFilesize);
                        writer.Write(entry.rawFilesize);
                        writer.Write(entry.chunkIdx);
                        writer.Write(entry.chunkCount);
                        writer.Write(entry.offset);
                        writer.Write(entry.crc32);
                    }
                }

                // Write chunk section data
                using (BinaryWriter writer = new BinaryWriter(chunksSection, Encoding.ASCII, true))
                {
                    foreach (var entry in chunkEntries)
                    {
                        writer.Write(entry.chunkSize);
                        writer.Write(entry.unk);
                        writer.Write(entry.chunkId);
                    }
                }

                Console.WriteLine("Writing {0}...", outputFilename);


                L7CAHeader header = new L7CAHeader();
                header.metadataOffset = (int)(archiveWriter.BaseStream.Position);
                header.archiveSize = (int)(header.metadataOffset + filesystemSection.Length + fileEntriesSection.Length + chunksSection.Length + stringSection.Length);
                header.metadataSize = (int)(filesystemSection.Length + fileEntriesSection.Length + chunksSection.Length + stringSection.Length);
                header.filesystemEntries = filesystemEntries.Count;
                header.folders = folders.Count;
                header.files = files.Count;
                header.chunks = chunkEntries.Count;
                header.stringTableSize = (int)stringSection.Length;
                archiveWriter.BaseStream.Seek(0, SeekOrigin.Begin);
                archiveWriter.Write(header.magic);
                archiveWriter.Write(header.unk);
                archiveWriter.Write(header.archiveSize);
                archiveWriter.Write(header.metadataOffset);
                archiveWriter.Write(header.metadataSize);
                archiveWriter.Write(header.unk2);
                archiveWriter.Write(header.filesystemEntries);
                archiveWriter.Write(header.folders);
                archiveWriter.Write(header.files);
                archiveWriter.Write(header.chunks);
                archiveWriter.Write(header.stringTableSize);
                archiveWriter.Write(header.unk4);

                archiveWriter.BaseStream.Seek(0, SeekOrigin.End);

                // Write file info sections
                archiveWriter.Write(filesystemSection.GetBuffer(), 0, (int)filesystemSection.Length);
                archiveWriter.Write(fileEntriesSection.GetBuffer(), 0, (int)fileEntriesSection.Length);
                archiveWriter.Write(chunksSection.GetBuffer(), 0, (int)chunksSection.Length);
                archiveWriter.Write(stringSection.GetBuffer(), 0, (int)stringSection.Length);

            }
        }

        static void UnpackL7CA(string inputFilename)
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(inputFilename)))
            {
                L7CAHeader header = new L7CAHeader();
                if (reader.ReadUInt32() != 0x4143374c)
                {
                    Console.WriteLine("Not a L7CA archive");
                    Environment.Exit(1);
                }

                header.unk = reader.ReadUInt32();
                header.archiveSize = reader.ReadInt32();
                header.metadataOffset = reader.ReadInt32();
                header.metadataSize = reader.ReadInt32();
                header.unk2 = reader.ReadUInt32();
                header.filesystemEntries = reader.ReadInt32();
                header.folders = reader.ReadInt32();
                header.files = reader.ReadInt32();
                header.chunks = reader.ReadInt32();
                header.stringTableSize = reader.ReadInt32();
                header.unk4 = reader.ReadInt32();


                // Read strings
                var baseOffset = reader.BaseStream.Length - header.stringTableSize;
                reader.BaseStream.Seek(baseOffset, SeekOrigin.Begin);

                Dictionary<int, string> strings = new Dictionary<int, string>();
                while (reader.PeekChar() != -1)
                {
                    int offset = (int)(reader.BaseStream.Position - baseOffset);
                    int len = reader.ReadByte();
                    var str = Encoding.UTF8.GetString(reader.ReadBytes(len));

                    strings.Add(offset, str);
                }


                // Read filesystem entries
                Dictionary<uint, L7CAFilesystemEntry> entries = new Dictionary<uint, L7CAFilesystemEntry>();
                reader.BaseStream.Seek(header.metadataOffset, SeekOrigin.Begin);
                for (int i = 0; i < header.filesystemEntries; i++)
                {
                    L7CAFilesystemEntry entry = new L7CAFilesystemEntry();
                    entry.id = reader.ReadUInt32();
                    entry.hash = reader.ReadUInt32();
                    entry.folderOffset = reader.ReadInt32();
                    entry.filenameOffset = reader.ReadInt32();
                    entry.timestamp = reader.ReadInt64();

                    if(entry.id == 0xffffffff)
                        entry.filename = String.Format("{0}", strings[entry.folderOffset]);
                    else
                        entry.filename = String.Format("{0}/{1}", strings[entry.folderOffset], strings[entry.filenameOffset]);

                    //Console.WriteLine("{0:x8} {1:x8} {2:x8} {3:x8} {4:x16}", entry.id, entry.hash, entry.folderOffset, entry.filenameOffset, entry.timestamp);

                    if (Crc32.CalculateNamco(entry.filename) != entry.hash)
                    {
                        Console.WriteLine("{0} did not match expected hash", entry.filename);
                    }

                    if (entry.id != 0xffffffff)
                    {
                        entries.Add(entry.id, entry);
                    }
                    else
                    {
                        // 0xffffffff is a folder.
                        // Only create a folder and move on to next entry.
                        // This step probably isn't needed,  but just for the sake of completeness I added it.
                        // There might be some game out there that has blank folders but no actual data in it, so those will be accounted for as well.

                        if (!Directory.Exists(entry.filename))
                            Directory.CreateDirectory(entry.filename);
                    }
                }

                // Read file information
                List<L7CAFileEntry> files = new List<L7CAFileEntry>();
                for (int i = 0; i < header.files; i++)
                {
                    L7CAFileEntry entry = new L7CAFileEntry();
                    entry.compressedFilesize = reader.ReadInt32();
                    entry.rawFilesize = reader.ReadInt32();
                    entry.chunkIdx = reader.ReadInt32();
                    entry.chunkCount = reader.ReadInt32();
                    entry.offset = reader.ReadInt32();
                    entry.crc32 = reader.ReadUInt32();

                    //var filename = entries[(uint)i].filename;
                    //Console.WriteLine("{2}\noffset[{0:x8}] filesize[{1:x8}]\nreal_crc32[{3:x8}] crc32[{4:x8}] crc32[{5:x8}]\n", entry.offset, entry.compressedFilesize, filename, entries[(uint)i].hash, crc32.Value, entry.crc32);
                    
                    files.Add(entry);
                }

                // Read chunk information
                List<L7CAChunkEntry> chunks = new List<L7CAChunkEntry>();
                for (int i = 0; i < header.chunks; i++)
                {
                    L7CAChunkEntry entry = new L7CAChunkEntry();
                    entry.chunkSize = reader.ReadInt32();
                    entry.unk = reader.ReadUInt16();
                    entry.chunkId = reader.ReadUInt16();

                    //Console.WriteLine("{3:x8} {0:x8} {1:x4} {2:x4}", entry.chunkSize, entry.unk, entry.chunkNum, i);

                    chunks.Add(entry);
                }

                for (int i = 0; i < header.files; i++)
                {
                    var file = files[i];
                    var entry = entries[(uint)i];

                    //DEBUG_DECOMP = true;

                    Console.WriteLine("Extracting {0}...", entry.filename);
                    //Console.WriteLine("{0:x1} {1:x8} {2:x8} {3:x8} {4:x8} {5:x8}", file.chunkIdx, file.chunkCount, file.offset, file.compressedFilesize, file.rawFilesize, file.crc32);

                    //var output = Path.Combine("output", entry.filename);
                    var output = entry.filename;
                    Directory.CreateDirectory(Path.GetDirectoryName(output));

                    reader.BaseStream.Seek(file.offset, SeekOrigin.Begin);
                    var orig_data = reader.ReadBytes(file.compressedFilesize);

                    //MemoryStream data = new MemoryStream();
                    List<byte> data = new List<byte>();

                    using (BinaryReader datastream = new BinaryReader(new MemoryStream(orig_data)))
                    {
                        if (DebugDecompressionCode)
                        {
                            foreach (var f in Directory.EnumerateFiles(".", "output-chunk-*.bin"))
                                File.Delete(f);
                        }

                        for (int x = 0; x < file.chunkCount; x++)
                        {
                            int len = chunks[file.chunkIdx + x].chunkSize & 0x7fffffff;
                            bool isCompressed = (chunks[file.chunkIdx + x].chunkSize & 0x80000000) != 0;

                            if (DebugDecompressionCode)
                                Console.WriteLine("{0:x8} {1}", len, isCompressed);

                            var d = datastream.ReadBytes(len);

                            if (DebugDecompressionCode)
                                File.WriteAllBytes(String.Format("output-chunk-{0}.bin", x), d);

                            if (isCompressed)
                            {
                                // Decompress chunk
                                d = Decompress(d, data.ToArray());
                                data = new List<byte>(d);
                            }
                            else
                            {
                                data.AddRange(d);
                            }

                            //Console.WriteLine(" {0:x8}", d.Length);

                            if (DebugDecompressionCode)
                                File.WriteAllBytes(String.Format("output-chunk-{0}-decomp.bin", x), d);
                        }
                    }

                    var crc32 = Crc32.Calculate(data.ToArray());
                    if (crc32 != file.crc32)
                    {
                        Console.WriteLine("Invalid CRC32: {0:x8} vs {1:x8}", crc32, file.crc32);
                        Console.WriteLine();
                        File.WriteAllBytes("invalid.bin", data.ToArray());

                        //Environment.Exit(1);
                    }

                    File.WriteAllBytes(output, data.ToArray());

                    if (data.Count != file.rawFilesize)
                    {
                        Console.WriteLine("Invalid filesize: {0:x8} vs {1:x8}", data.Count, file.rawFilesize);
                        Environment.Exit(1);
                    }
                }
            }
        }
    }
}
