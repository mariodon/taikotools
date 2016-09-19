using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace GXTConvert.FileFormat
{
    public abstract class SceGxtTextureInfo
    {
        public uint DataOffset { get; set; }
        public uint DataSize { get; set; }
        public int PaletteIndex { get; private set; }
        public uint Flags { get; private set; }
        public uint[] ControlWords { get; set; }

        public abstract SceGxmTextureType GetTextureType();
        public abstract SceGxmTextureFormat GetTextureFormat();
        public abstract ushort GetWidth();
        public abstract ushort GetHeight();

        //TODO: where's byteStride for texture type LinearStrided?

        public SceGxmTextureBaseFormat GetTextureBaseFormat()
        {
            return (SceGxmTextureBaseFormat)((uint)GetTextureFormat() & 0xFFFF0000);
        }

        public ushort GetWidthRounded()
        {
            int roundedWidth = 1;
            while (roundedWidth < GetWidth()) roundedWidth *= 2;
            return (ushort)roundedWidth;
        }

        public ushort GetHeightRounded()
        {
            int roundedHeight = 1;
            while (roundedHeight < GetHeight()) roundedHeight *= 2;
            return (ushort)roundedHeight;
        }
    }

    public class SceGxtTextureInfoRaw : SceGxtTextureInfo
    {
        public SceGxtTextureInfoRaw(uint textureType, uint textureFormat, ushort width, ushort height, uint dataOffset, uint dataSize)
        {
            DataOffset = dataOffset;
            DataSize = dataSize;

            ControlWords = new uint[3];
            ControlWords[0] = textureType;
            ControlWords[1] = textureFormat;
            ControlWords[2] = ((uint)height << 16) | width;
        }

        public override SceGxmTextureType GetTextureType() { return (SceGxmTextureType)ControlWords[0]; }
        public override SceGxmTextureFormat GetTextureFormat() { return (SceGxmTextureFormat)ControlWords[1]; }
        public override ushort GetWidth() { return (ushort)(ControlWords[2] & 0xFFFF); }
        public override ushort GetHeight() { return (ushort)(ControlWords[2] >> 16); }
    }
}
