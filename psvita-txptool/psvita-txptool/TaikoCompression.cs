using System;
using System.Collections.Generic;
using System.IO;

namespace TaikoCompression
{
    class TaikoCompression
    {
        static bool DebugDecompressionCode = false;

        public static byte[] Compress(byte[] data)
        {
            List<byte> output = new List<byte>();

            using (BinaryReader reader = new BinaryReader(new MemoryStream(data)))
            {
                int remaining = data.Length;
                while (remaining > 0)
                {
                    byte compLen = 0x3f;

                    if (remaining < compLen)
                        compLen = (byte)remaining;

                    output.Add(compLen);
                    output.AddRange(reader.ReadBytes(compLen));

                    remaining -= compLen;
                }
            }

            for (int i = 0; i < 3; i++)
                output.Add(0);

            return output.ToArray();
        }

        public static byte[] Decompress(byte[] data, byte[] prev = null)
        {
            List<byte> output = new List<byte>();

            if (prev != null)
                output = new List<byte>(prev);

            using (BinaryReader reader = new BinaryReader(new MemoryStream(data)))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    byte c = reader.ReadByte();

                    if (DebugDecompressionCode)
                        Console.Write("{0:x8} {1:x2} | {2:x8} | ", reader.BaseStream.Position - 1, c, output.Count);

                    if (c > 0xbf)
                    {
                        int len = (c - 0xbe) * 2;
                        int flag = reader.ReadByte();
                        int back = ((flag & 0x7f) << 8) + reader.ReadByte() + 1;

                        if ((flag & 0x80) != 0)
                        {
                            len += 1;
                        }

                        if (DebugDecompressionCode)
                            Console.WriteLine("{0:x4} {1:x4} | {2:x4}", len, back, flag);

                        int end = output.Count;
                        for (int i = 0; i < len; i++)
                        {
                            output.Add(output[end - back + i]);
                        }
                    }
                    else if (c > 0x7f)
                    {
                        int len = ((c >> 2) & 0x1f);
                        int back = ((c & 0x3) << 8) + reader.ReadByte() + 1;

                        if ((c & 0x80) != 0)
                        {
                            len += 3;
                        }

                        if (DebugDecompressionCode)
                            Console.WriteLine("{0:x4} {1:x4}", len, back);

                        int end = output.Count;
                        for (int i = 0; i < len; i++)
                        {
                            if (i > end)
                            {
                                output.Add(output[end - 1]);
                            }
                            else
                            {
                                output.Add(output[end - back + i]);
                            }
                        }
                    }
                    else if (c > 0x3f)
                    {
                        int len = (c >> 4) - 2;
                        int back = (c & 0x0f) + 1;

                        if (DebugDecompressionCode)
                            Console.WriteLine("{0:x4} {1:x4}", len, back);

                        int end = output.Count;
                        for (int i = 0; i < len; i++)
                        {
                            if (i > end)
                            {
                                output.Add(output[end - 1]);
                            }
                            else
                            {
                                output.Add(output[end - back + i]);
                            }
                        }
                    }
                    else if (c == 0x00)
                    {
                        var offset = reader.BaseStream.Position - 1;

                        // Wat?
                        int flag = reader.ReadByte();
                        int flag2 = 0;
                        int len = 0x40;

                        if ((flag & 0x80) == 0)
                        {
                            flag2 = reader.ReadByte();

                            len = 0xbf + flag2 + (flag << 8);

                            if (flag == 0 && flag2 == 0 && reader.PeekChar() == 0x00)
                            {
                                break;
                            }
                        }
                        else
                        {
                            len += (flag & 0x7f);
                        }

                        if (DebugDecompressionCode)
                            Console.WriteLine("{0:x2} {1:x2} ({2:x2}) @ {3:x8}", len, flag, flag2, offset);

                        output.AddRange(reader.ReadBytes(len));
                    }
                    else
                    {
                        if (DebugDecompressionCode)
                            Console.WriteLine("");

                        output.AddRange(reader.ReadBytes(c));
                    }

                    if (DebugDecompressionCode)
                        File.WriteAllBytes("output.bin", output.ToArray());
                }
            }

            return output.ToArray();
        }
    }
}
