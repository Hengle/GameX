using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GameX.Bullfrog.Formats
{
    #region Rnc

    public unsafe class Rnc
    {
        #region Header

        public const uint RNC_MAGIC = 0x01434e52;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct RNC_Header
        {
            public static (string, int) Struct = (">4x2I3H", sizeof(RNC_Header));
            public uint Signature;
            public uint UnpackedSize;
            public uint PackedSize;
            public ushort UnpackedCrc32;
            public ushort PackedCrc32;
            public ushort Unknown;

            internal byte[] Unpack(BinaryReader r)
            {
                if ((UnpackedSize > (1 << 30)) || (PackedSize > (1 << 30))) throw new FormatException("Bad Size");
                var input = r.ReadBytes((int)PackedSize);
                if (RncCrc16.Compute(input) != PackedCrc32) throw new FormatException("Bad CRC");
                var output = Unpack(input, (int)UnpackedSize);
                if (RncCrc16.Compute(output) != UnpackedCrc32) throw new FormatException("Bad CRC");
                return output;
            }

            static byte[] Unpack(byte[] input, int unpackedSize)
            {
                var output = new byte[unpackedSize];

                var bs = new BitStream(input);
                bs.Advance(2); // discard first two bits

                int o = 0;
                int oend = output.Length;

                // Process chunks
                var raw = new HufTable();
                var dist = new HufTable();
                var len = new HufTable();
                while (o < oend)
                {
                    uint chCount;
                    if (bs.Remain < 6) throw new Exception();

                    raw.ReadTable(bs);
                    dist.ReadTable(bs);
                    len.ReadTable(bs);
                    chCount = bs.Read(0xFFFF, 16);

                    while (true)
                    {
                        var length = raw.Read(bs);
                        if (length == -1) throw new Exception();
                        else if (length != 0)
                        {
                            while (length-- != 0)
                            {
                                //if (bs.Remain <= 0 || (o >= oend)) throw new Exception();
                                output[o++] = bs.ReadByte();
                            }
                            bs.Fix();
                        }
                        if (--chCount <= 0) break;

                        var posn = dist.Read(bs);
                        if (posn == -1) throw new Exception();
                        length = len.Read(bs);
                        if (length == -1) throw new Exception();

                        posn += 1;
                        length += 2;
                        while (length-- != 0)
                        {
                            //if (((o - posn) > oend) || (o > oend)) throw new Exception();
                            output[o] = output[o - posn];
                            o++;
                        }
                    }
                }
                return output;
            }
        }

        #endregion

        #region Hufman

        class HufTable
        {
            public int Num = 0;  // number of nodes in the tree
            public (uint code, int codelen, int value)[] Table = new (uint code, int codelen, int value)[32];

            // Read a Huffman table out of the bit stream and data stream given.
            public void ReadTable(BitStream bs)
            {
                int i;
                var leaflen = stackalloc int[32];
                // big-endian form of code
                var num = (int)bs.Read(0x1F, 5);
                if (num == 0) return;

                var leafmax = 1;
                for (i = 0; i < num; i++)
                {
                    leaflen[i] = (int)bs.Read(0x0F, 4);
                    if (leafmax < leaflen[i]) leafmax = leaflen[i];
                }

                var codeb = 0U;
                var k = 0;
                for (i = 1; i <= leafmax; i++)
                {
                    for (var j = 0; j < num; j++)
                        if (leaflen[j] == i)
                        {
                            Table[k].code = Mirror(codeb, i);
                            Table[k].codelen = i;
                            Table[k].value = j;
                            codeb++;
                            k++;
                        }
                    codeb <<= 1;
                }
                Num = k;
            }

            // Read a value out of the bit stream using the given Huffman table.
            public int Read(BitStream bs)
            {
                int i;
                for (i = 0; i < Num; i++)
                {
                    var mask = (uint)((1 << Table[i].codelen) - 1);
                    if (bs.Peek(mask) == Table[i].code) break;
                }
                if (i == Num) return -1;
                bs.Advance(Table[i].codelen);

                var val = (uint)Table[i].value;
                if (val >= 2)
                {
                    val = (uint)(1 << (int)(val - 1));
                    val |= bs.Read(val - 1, Table[i].value - 1);
                }
                return (int)val;
            }

            // Mirror the bottom n bits of x.
            static uint Mirror(uint x, int n)
            {
                var top = (uint)(1 << (n - 1));
                var bottom = 1U;
                while (top > bottom)
                {
                    var mask = top | bottom;
                    var masked = x & mask;
                    if (masked != 0 && masked != mask) x ^= mask;
                    top >>= 1;
                    bottom <<= 1;
                }
                return x;
            }
        }

        #endregion

        #region RncCrc16

        public unsafe static class RncCrc16
        {
            static ushort[] crctab = makeCrcTable();

            static ushort[] makeCrcTable()
            {
                ushort val;
                var r = new ushort[256];
                for (ushort i = 0; i < 256; i++)
                {
                    val = i;
                    for (var j = 0; j < 8; j++)
                        val = (val & 1) != 0
                            ? (ushort)((val >> 1) ^ 0xA001)
                            : (ushort)(val >> 1);
                    r[i] = val;
                }
                return r;
            }

            // Calculate a CRC, the RNC way
            public static int Compute(byte[] data)
            {
                var len = data.Length;
                fixed (byte* _ = data)
                {
                    var p = _;
                    ushort val = 0;
                    while (len-- != 0)
                    {
                        val ^= *p++;
                        val = (ushort)((val >> 8) ^ crctab[val & 0xFF]);
                    }
                    return val;
                }
            }
        }

        #endregion

        public static byte[] Read(BinaryReader r)
        {
            var header = r.ReadS<RNC_Header>();
            if (header.Signature == RNC_MAGIC) return header.Unpack(r);
            r.BaseStream.Position = 0;
            return r.ReadToEnd();
        }
    }

    #endregion
}