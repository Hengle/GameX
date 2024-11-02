using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Threading.Tasks;
using static OpenStack.Debug;

namespace GameX.Volition.Formats
{
    // https://falloutmods.fandom.com/wiki/MVE_File_Format#Header
    // https://wiki.multimedia.cx/index.php/Interplay_MVE
    public class Binary_Mvl : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Mvl(r));

        enum Chunk : ushort
        {
            INIT_AUDIO = 0x0000,
            AUDIO_ONLY = 0x0001,
            INIT_VIDEO = 0x0002,
            VIDEO = 0x0003,
            SHUTDOWN = 0x0004,
            END = 0x0005,
        }

        enum Opcode : byte
        {
            END_OF_STREAM = 0x00,
            END_OF_CHUNK = 0x01,
            CREATE_TIMER = 0x02,
            INIT_AUDIO_BUFFERS = 0x03,
            START_STOP_AUDIO = 0x04,
            INIT_VIDEO_BUFFERS = 0x05,
            UNKNOWN_06 = 0x06,
            SEND_BUFFER = 0x07,
            AUDIO_FRAME = 0x08,
            SILENCE_FRAME = 0x09,
            INIT_VIDEO_MODE = 0x0A,
            CREATE_GRADIENT = 0x0B,
            SET_PALETTE = 0x0C,
            SET_PALETTE_COMPRESSED = 0x0D,
            UNKNOWN_0E = 0x0E,
            SET_DECODING_MAP = 0x0F,
            UNKNOWN_10 = 0x10,
            VIDEO_DATA = 0x11,
            UNKNOWN_12 = 0x12,
            UNKNOWN_13 = 0x13,
            UNKNOWN_14 = 0x14,
            UNKNOWN_15 = 0x15,
        }

        public Binary_Mvl(BinaryReader r)
        {
            const int MAGIC = 0x4c564d44;
            const string SIGNATURE = "Interplay MVE File\x1A\x00";

            var magic = r.ReadUInt32();
            if (magic != MAGIC) throw new FormatException("BAD MAGIC");
            var count = r.ReadUInt32();
            r.Seek(8 + (17 * count));   // past sig+metadata.

            // check the header
            if (r.ReadFAString(20) != SIGNATURE) throw new FormatException("BAD MAGIC");

            // skip the next 6 bytes
            r.Skip(6);

            int chunkSize;
            Chunk chunkType = 0;
            Opcode opcodeType;
            byte opcodeVersion;
            int opcodeSize;

            // iterate through the chunks in the file
            while (chunkType != Chunk.END)
            {
                chunkSize = r.ReadUInt16();
                chunkType = (Chunk)r.ReadUInt16();
                Log($"\nchunk type {chunkType}, {chunkSize} bytes: ");
                switch (chunkType)
                {
                    case Chunk.INIT_AUDIO:
                        Log("initialize audio");
                        break;

                    case Chunk.AUDIO_ONLY:
                        Log("audio only");
                        break;

                    case Chunk.INIT_VIDEO:
                        Log("initialize video");
                        break;

                    case Chunk.VIDEO:
                        Log("video (and audio)");
                        break;

                    case Chunk.SHUTDOWN:
                        Log("shutdown");
                        break;

                    case Chunk.END:
                        Log("end");
                        break;

                    default:
                        Log(" *** unknown chunk type");
                        break;
                }

                Log("------------------------------------------------------\n");

                // iterate through individual opcodes
                while (chunkSize > 0)
                {
                    opcodeSize = r.ReadUInt16();
                    opcodeType = (Opcode)r.ReadByte();
                    opcodeVersion = r.ReadByte();
                    chunkSize -= 4 - opcodeSize;
                    Log($"  opcode type {opcodeType}, version {opcodeVersion}, {opcodeSize} bytes: ");
                    switch (opcodeType)
                    {
                        case Opcode.END_OF_STREAM:
                            Log("end of stream");
                            break;

                        case Opcode.END_OF_CHUNK:
                            Log("end of chunk");
                            break;

                        case Opcode.CREATE_TIMER:
                            Log("create timer");
                            break;

                        case Opcode.INIT_AUDIO_BUFFERS:
                            Log("initialize audio buffers");
                            break;

                        case Opcode.START_STOP_AUDIO:
                            Log("start/stop audio\n");
                            break;

                        case Opcode.INIT_VIDEO_BUFFERS:
                            Log("initialize video buffers\n");
                            break;

                        case Opcode.UNKNOWN_06:
                        case Opcode.UNKNOWN_0E:
                        case Opcode.UNKNOWN_10:
                        case Opcode.UNKNOWN_12:
                        case Opcode.UNKNOWN_13:
                        case Opcode.UNKNOWN_14:
                        case Opcode.UNKNOWN_15:
                            Log($"unknown (but documented) opcode {opcodeType}");
                            break;

                        case Opcode.SEND_BUFFER:
                            Log("send buffer");
                            break;

                        case Opcode.AUDIO_FRAME:
                            Log("audio frame");
                            break;

                        case Opcode.SILENCE_FRAME:
                            Log("silence frame");
                            break;

                        case Opcode.INIT_VIDEO_MODE:
                            Log("initialize video mode");
                            break;

                        case Opcode.CREATE_GRADIENT:
                            Log("create gradient");
                            break;

                        case Opcode.SET_PALETTE:
                            Log("set palette");
                            break;

                        case Opcode.SET_PALETTE_COMPRESSED:
                            Log("set palette compressed");
                            break;

                        case Opcode.SET_DECODING_MAP:
                            Log("set decoding map");
                            break;

                        case Opcode.VIDEO_DATA:
                            Log("set video data");
                            break;

                        default:
                            Log(" *** unknown opcode type");
                            break;
                    }

                    // skip over the meat of the opcode
                    r.Skip(opcodeSize);
                }
            }
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = this }),
            new MetaInfo($"{nameof(Binary_Mvl)}", items: new List<MetaInfo> {
                //new MetaInfo($"abc: {abc}"),
            })
        };
    }
}
