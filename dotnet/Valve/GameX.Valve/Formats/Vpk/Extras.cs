using GameX.Algorithms;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Decoder = SevenZip.Compression.LZMA.Decoder;

namespace GameX.Valve.Formats.Vpk
{
    #region ClosedCaption

    public class ClosedCaptions : IEnumerable<ClosedCaptions.ClosedCaption>, IHaveMetaInfo
    {
        public const int MAGIC = 0x44434356; // "VCCD"

        public class ClosedCaption
        {
            public uint Hash;
            public uint UnknownV2;
            public int Blocknum;
            public ushort Offset;
            public ushort Length;
            public string Text;
        }

        public ClosedCaptions() { }
        public ClosedCaptions(BinaryReader r) => Read(r);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "DataGrid", Name = "Captions", Value = Captions }),
            new("ClosedCaptions", items: [
                new($"Count: {Captions.Count}"),
            ]),
        ];

        public List<ClosedCaption> Captions = [];

        public IEnumerator<ClosedCaption> GetEnumerator() => ((IEnumerable<ClosedCaption>)Captions).GetEnumerator();

        public ClosedCaption this[string key]
        {
            get
            {
                var hash = Crc32Digest.Compute(Encoding.UTF8.GetBytes(key));
                return Captions.Find(caption => caption.Hash == hash);
            }
        }

        public void Read(BinaryReader r)
        {
            if (r.ReadUInt32() != MAGIC) throw new InvalidDataException("Given file is not a VCCD.");

            var version = r.ReadUInt32();
            if (version != 1 && version != 2) throw new InvalidDataException($"Unsupported VCCD version: {version}");

            // numblocks, not actually required for hash lookups or populating entire list
            r.ReadUInt32();
            var blocksize = r.ReadUInt32();
            var directorysize = r.ReadUInt32();
            var dataoffset = r.ReadUInt32();
            for (var i = 0U; i < directorysize; i++)
                Captions.Add(new ClosedCaption
                {
                    Hash = r.ReadUInt32(),
                    UnknownV2 = version >= 2 ? r.ReadUInt32() : 0,
                    Blocknum = r.ReadInt32(),
                    Offset = r.ReadUInt16(),
                    Length = r.ReadUInt16()
                });

            // Probably could be inside the for loop above, but I'm unsure what the performance costs are of moving the position head manually a bunch compared to reading sequentually
            foreach (var caption in Captions)
            {
                r.Seek(dataoffset + caption.Blocknum * blocksize + caption.Offset);
                caption.Text = r.ReadZEncoding(Encoding.Unicode);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ClosedCaption>)Captions).GetEnumerator();
    }

    #endregion

    #region CompiledShader

    public class CompiledShader : IHaveMetaInfo
    {
        public const int MAGIC = 0x32736376; // "vcs2"

        string ShaderType;
        string ShaderPlatform;
        string Shader;

        public CompiledShader() { }
        public CompiledShader(BinaryReader r, string filename) => Read(r, filename);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Text", Name = "Shader", Value = Shader }),
            new("CompiledShader", items: [
                new($"ShaderType: {ShaderType}"),
                new($"ShaderPlatform: {ShaderPlatform}"),
            ]),
        ];

        public void Read(BinaryReader r, string filename)
        {
            var b = new StringBuilder();
            if (filename.EndsWith("vs.vcs")) ShaderType = "vertex";
            else if (filename.EndsWith("ps.vcs")) ShaderType = "pixel";
            else if (filename.EndsWith("features.vcs")) ShaderType = "features";
            if (filename.Contains("vulkan")) ShaderPlatform = "vulkan";
            else if (filename.Contains("pcgl")) ShaderPlatform = "opengl";
            else if (filename.Contains("pc_")) ShaderPlatform = "directx";
            if (r.ReadUInt32() != MAGIC) throw new FormatException("Given file is not a vcs2.");

            // Known versions:
            //  62 - April 2016
            //  63 - March 2017
            //  64 - May 2017
            var version = r.ReadUInt32();
            if (version != 64) throw new FormatException($"Unsupported VCS2 version: {version}");
            if (ShaderType == "features") ReadFeatures(r, b);
            else ReadShader(r, b);
            Shader = b.ToString();
        }

        void ReadFeatures(BinaryReader r, StringBuilder b_)
        {
            var anotherFileRef = r.ReadInt32(); // new in version 64, mostly 0 but sometimes 1

            var wtf = r.ReadUInt32(); // appears to be 0 in 'features'
            b_.AppendLine($"wtf: {wtf}");

            var name = Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
            r.ReadByte(); // null term?

            b_.AppendLine($"Name: {name} - Offsets: {r.BaseStream.Position}");

            var a = r.ReadInt32();
            var b = r.ReadInt32();
            var c = r.ReadInt32();
            var d = r.ReadInt32();
            var e = r.ReadInt32();
            var f = r.ReadInt32();
            var g = r.ReadInt32();
            var h = r.ReadInt32();
            if (anotherFileRef == 1) { var i = r.ReadInt32(); b_.AppendLine($"{a} {b} {c} {d} {e} {f} {g} {h} {i}"); }
            else b_.AppendLine($"{a} {b} {c} {d} {e} {f} {g} {h}");
            var count = r.ReadUInt32();
            long prevPos;
            b_.AppendLine($"Count: {count}");
            for (var i = 0; i < count; i++)
            {
                prevPos = r.BaseStream.Position;

                name = r.ReadVUString();
                r.Seek(prevPos + 128);

                var type = r.ReadUInt32();
                b_.AppendLine($"Name: {name} - Type: {type} - Offsets: {r.BaseStream.Position}");

                if (type == 1)
                {
                    prevPos = r.BaseStream.Position;
                    var subname = r.ReadVUString();
                    b_.AppendLine(subname);
                    r.BaseStream.Position = prevPos + 64;
                    r.ReadUInt32();
                }
            }

            var identifierCount = 8;
            if (anotherFileRef == 1) identifierCount++;

            // Appears to be always 128 bytes in version 63 and higher, 112 before
            for (var i = 0; i < identifierCount; i++)
            {
                // either 6 or 7 is cs (compute shader)
                // 0 - ?
                // 1 - vertex shader
                // 2 - pixel shader
                // 3 - geometry shader
                // 4 - hull shader
                // 5 - domain shader
                // 6 - ?
                // 7 - ?, new in version 63
                // 8 - pixel shader render state (only if uint in version 64+ at pos 8 is 1)
                var identifier = r.ReadBytes(16);
                b_.AppendLine($"#{i} identifier: {BitConverter.ToString(identifier)}");
            }

            r.ReadUInt32(); // 0E 00 00 00

            count = r.ReadUInt32();
            for (var i = 0; i < count; i++)
            {
                prevPos = r.BaseStream.Position;
                name = r.ReadVUString();
                r.BaseStream.Position = prevPos + 64;
                prevPos = r.BaseStream.Position;
                var desc = r.ReadVUString();
                r.BaseStream.Position = prevPos + 84;
                var subcount = r.ReadUInt32();
                b_.AppendLine($"Name: {name} - Desc: {desc} - Count: {subcount} - Offsets: {r.BaseStream.Position}");
                for (var j = 0; j < subcount; j++) b_.AppendLine($"     {r.ReadVUString()}");
            }

            count = r.ReadUInt32();
            b_.AppendLine($"Count: {count}");
        }

        void ReadShader(BinaryReader r, StringBuilder b_)
        {
            // This uint controls whether or not there's an additional uint and file identifier in header for features shader, might be something different in these.
            var unk0_a = r.ReadInt32(); // new in version 64, mostly 0 but sometimes 1

            var fileIdentifier = r.ReadBytes(16);
            var staticIdentifier = r.ReadBytes(16);

            b_.AppendLine($"File identifier: {BitConverter.ToString(fileIdentifier)}");
            b_.AppendLine($"Static identifier: {BitConverter.ToString(staticIdentifier)}");

            var unk0_b = r.ReadUInt32();
            b_.AppendLine($"wtf {unk0_b}"); // Always 14?

            // Chunk 1
            var count = r.ReadUInt32();
            b_.AppendLine($"[CHUNK 1] Count: {count} - Offsets: {r.BaseStream.Position}");

            for (var i = 0; i < count; i++)
            {
                var previousPosition = r.BaseStream.Position;
                var name = r.ReadVUString();
                r.BaseStream.Position = previousPosition + 128;

                var unk1_a = r.ReadInt32();
                var unk1_b = r.ReadInt32();
                var unk1_c = r.ReadInt32();
                var unk1_d = r.ReadInt32();
                var unk1_e = r.ReadInt32();
                var unk1_f = r.ReadInt32();
                b_.AppendLine($"{unk1_a} {unk1_b} {unk1_c} {unk1_d} {unk1_e} {unk1_f} {name}");
            }

            // Chunk 2 - Similar structure to chunk 4, same chunk size
            count = r.ReadUInt32();
            b_.AppendLine($"[CHUNK 2] Count: {count} - Offsets: {r.BaseStream.Position}");

            for (var i = 0; i < count; i++)
            {
                // Initial research based on brushsplat_pc_40_ps, might be different for other shaders
                var unk2_a = r.ReadUInt32(); // always 3?
                var unk2_b = r.ReadUInt32(); // always 2?
                var unk2_c = r.ReadUInt16(); // always 514?
                var unk2_d = r.ReadUInt16(); // always 514?
                var unk2_e = r.ReadUInt32();
                var unk2_f = r.ReadUInt32();
                var unk2_g = r.ReadUInt32();
                var unk2_h = r.ReadUInt32();
                var unk2_i = r.ReadUInt32();
                var unk2_j = r.ReadUInt32();
                var unk2_k = r.ReadUInt32();
                r.ReadBytes(176); // Chunk of mostly FF
                r.ReadBytes(256); // Chunk of 0s. padding?
                b_.AppendLine($"{unk2_a} {unk2_b} {unk2_c} {unk2_d} {unk2_e} {unk2_f} {unk2_g} {unk2_h} {unk2_i} {unk2_j} {unk2_k}");
            }

            // 3
            count = r.ReadUInt32();
            b_.AppendLine($"[CHUNK 3] Count: {count} - Offsets: {r.BaseStream.Position}");

            for (var i = 0; i < count; i++)
            {
                var previousPosition = r.BaseStream.Position;
                var name = r.ReadVUString();
                r.BaseStream.Position = previousPosition + 128;

                var unk3_a = r.ReadInt32();
                var unk3_b = r.ReadInt32();
                var unk3_c = r.ReadInt32();
                var unk3_d = r.ReadInt32();
                var unk3_e = r.ReadInt32();
                var unk3_f = r.ReadInt32();
                b_.AppendLine($"{unk3_a} {unk3_b} {unk3_c} {unk3_d} {unk3_e} {unk3_f} {name}");
            }

            // 4 - Similar structure to chunk 2, same chunk size
            count = r.ReadUInt32();
            b_.AppendLine($"[CHUNK 4] Count: {count} - Offsets: {r.BaseStream.Position}");

            for (var i = 0; i < count; i++)
            {
                var unk4_a = r.ReadUInt32();
                var unk4_b = r.ReadUInt32();
                var unk4_c = r.ReadUInt16();
                var unk4_d = r.ReadUInt16();
                var unk4_e = r.ReadUInt32();
                var unk4_f = r.ReadUInt32();
                var unk4_g = r.ReadUInt32();
                var unk4_h = r.ReadUInt32();
                var unk4_i = r.ReadUInt32();

                r.ReadBytes(184); // Chunk of mostly FF
                r.ReadBytes(256); // Chunk of 0s. padding?
                b_.AppendLine($"{unk4_a} {unk4_b} {unk4_c} {unk4_d} {unk4_e} {unk4_f} {unk4_g} {unk4_h} {unk4_i}");
            }

            // 5 - Globals?
            count = r.ReadUInt32();
            b_.AppendLine($"[CHUNK 5] Count: {count} - Offsets: {r.BaseStream.Position}");

            for (var i = 0; i < count; i++)
            {
                var previousPosition = r.BaseStream.Position;
                var name = r.ReadVUString();
                r.BaseStream.Position = previousPosition + 128; // ??

                var hasDesc = r.ReadInt32();
                var unk5_a = r.ReadInt32();

                var desc = string.Empty;

                if (hasDesc > 0)
                    desc = r.ReadVUString();

                r.BaseStream.Position = previousPosition + 200;
                var type = r.ReadInt32();
                var length = r.ReadInt32();
                r.BaseStream.Position = previousPosition + 480;

                // Don't know what content of this chunk is yet, but size seems to depend on type.
                // If we read the amount of bytes below per type the rest of the file will process as usual (and get to the LZMA stuff).
                // CHUNK SIZES:
                //  Type 0: 480
                //  Type 1: 480 + LENGTH + 4!
                //  Type 2: 480 (brushsplat_pc_40_ps.vcs)
                //  Type 5: 480 + LENGTH + 4! (debugoverlay_wireframe_pc_40_vs.vcs)
                //  Type 6: 480 + LENGTH + 4! (depth_only_pc_30_ps.vcs)
                //  Type 7: 480 + LENGTH + 4! (grasstile_preview_pc_41_ps.vcs)
                //  Type 10: 480 (brushsplat_pc_40_ps.vcs)
                //  Type 11: 480 (post_process_pc_30_ps.vcs)
                //  Type 13: 480 (spriteentity_pc_41_vs.vcs)
                // Needs further investigation. This is where parsing a lot of shaders break right now.
                if (length > -1 && type != 0 && type != 2 && type != 10 && type != 11 && type != 13)
                {
                    if (type != 1 && type != 5 && type != 6 && type != 7) b_.AppendLine($"!!! Unknown type of type {type} encountered at position {r.BaseStream.Position - 8}. Assuming normal sized chunk.");
                    else
                    {
                        var unk5_b = r.ReadBytes(length);
                        var unk5_c = r.ReadUInt32();
                    }
                }

                var unk5_d = r.ReadUInt32();
                b_.AppendLine($"{type} {length} {name} {hasDesc} {desc}");
            }

            // 6
            count = r.ReadUInt32();
            b_.AppendLine($"[CHUNK 6] Count: {count} - Offsets: {r.BaseStream.Position}");

            for (var i = 0; i < count; i++)
            {
                var unk6_a = r.ReadBytes(4); // unsure, maybe shorts or bytes
                var unk6_b = r.ReadUInt32(); // 12, 13, 14 or 15 in brushplat_pc_40_ps.vcs
                var unk6_c = r.ReadBytes(12); // FF
                var unk6_d = r.ReadUInt32();

                var previousPosition = r.BaseStream.Position;
                var name = r.ReadVUString();
                r.BaseStream.Position = previousPosition + 256;

                b_.AppendLine($"{unk6_b} {unk6_d} {name}");
            }

            // 7 - Input buffer layout
            count = r.ReadUInt32();
            b_.AppendLine($"[CHUNK 7] Count: {count} - Offsets: {r.BaseStream.Position}");

            for (var i = 0; i < count; i++)
            {
                var prevPos = r.BaseStream.Position;
                var name = r.ReadVUString(8);
                r.BaseStream.Position = prevPos + 64;

                var a = r.ReadUInt32();
                var b = r.ReadUInt32();
                var subCount = r.ReadUInt32();
                b_.AppendLine($"[SUB CHUNK] Name: {name} - unk1: {a} - unk2: {b} - Count: {subCount} - Offsets: {r.BaseStream.Position}");

                for (var j = 0; j < subCount; j++)
                {
                    var previousPosition = r.BaseStream.Position;
                    var subname = r.ReadVUString();
                    r.BaseStream.Position = previousPosition + 64;

                    var bufferOffset = r.ReadUInt32(); // Offset in the buffer
                    var components = r.ReadUInt32(); // Number of components in this element
                    var componentSize = r.ReadUInt32(); // Number of floats per component
                    var repetitions = r.ReadUInt32(); // Number of repetitions?
                    b_.AppendLine($"     Name: {subname} - offset: {bufferOffset} - components: {components} - compSize: {componentSize} - num: {repetitions}");
                }

                r.ReadBytes(4);
            }

            b_.AppendLine($"Offsets: {r.BaseStream.Position}");

            // Vertex shader has a string chunk which seems to be vertex buffer specifications
            if (ShaderType == "vertex")
            {
                var bufferCount = r.ReadUInt32();
                b_.AppendLine($"{bufferCount} vertex buffer descriptors");
                for (var h = 0; h < bufferCount; h++)
                {
                    count = r.ReadUInt32(); // number of attributes
                    b_.AppendLine($"Buffer #{h}, {count} attributes");

                    for (var i = 0; i < count; i++)
                    {
                        var name = r.ReadVUString();
                        var type = r.ReadVUString();
                        var option = r.ReadVUString();
                        var unk = r.ReadUInt32(); // 0, 1, 2, 13 or 14
                        b_.AppendLine($"     Name: {name}, Type: {type}, Option: {option}, Unknown uint: {unk}");
                    }
                }
            }

            var lzmaCount = r.ReadUInt32();
            b_.AppendLine($"Offsets: {r.BaseStream.Position}");

            var unkLongs = new long[lzmaCount];
            for (var i = 0; i < lzmaCount; i++) unkLongs[i] = r.ReadInt64();

            var lzmaOffsets = new int[lzmaCount];
            for (var i = 0; i < lzmaCount; i++) lzmaOffsets[i] = r.ReadInt32();

            for (var i = 0; i < lzmaCount; i++)
            {
                b_.AppendLine("Extracting shader {i}..");
                // File.WriteAllBytes(Path.Combine(@"D:\shaders\PCGL DotA Core\processed spritecard\", "shader_out_" + i + ".bin"), ReadShaderChunk(lzmaOffsets[i]));

                // Skip non-PCGL shaders for now, need to figure out platform without checking filename
                if (ShaderPlatform != "opengl") continue;

                // What follows here is super experimental and barely works as is. It is a very rough implementation to read and extract shader stringblocks for PCGL shaders.
                using var inputStream = new MemoryStream(ReadShaderChunk(r, b_, lzmaOffsets[i]));
                using var chunkReader = new BinaryReader(inputStream);
                while (chunkReader.BaseStream.Position < chunkReader.BaseStream.Length)
                {
                    // Read count that also doubles as mode?
                    var modeAndCount = chunkReader.ReadInt16();

                    // Mode never seems to be 20 for anything but the FF chunk before shader stringblock
                    if (modeAndCount != 20)
                    {
                        chunkReader.ReadInt16();
                        var unk2 = chunkReader.ReadInt32();
                        var unk3 = chunkReader.ReadInt32();

                        // If the mode isn't the same as unk3, skip shader for now
                        if (modeAndCount != unk3) { b_.AppendLine($"Having issues reading shader {i}, skipping.."); chunkReader.BaseStream.Position = chunkReader.BaseStream.Length; continue; }

                        chunkReader.ReadBytes(unk3 * 4);

                        var unk4 = chunkReader.ReadUInt16();

                        // Seems to be 1 if there's a string there, read 26 byte stringblock, roll back if not
                        if (unk4 == 1) chunkReader.ReadBytes(26);
                        else chunkReader.BaseStream.Position -= 2;
                    }
                    else if (modeAndCount == 20)
                    {
                        // Read 40 byte 0xFF chunk
                        chunkReader.ReadBytes(40);

                        // Read 5 unknown bytes
                        chunkReader.ReadBytes(5);

                        // Shader stringblock count
                        var shaderContentCount = chunkReader.ReadUInt32();

                        // Read trailing byte
                        chunkReader.ReadByte();

                        // If shader stringblock count is ridiculously high stop reading this shader and bail
                        if (shaderContentCount > 100) { b_.AppendLine($"Having issues reading shader {i}, skipping.."); chunkReader.BaseStream.Position = chunkReader.BaseStream.Length; continue; }

                        // Read and dump all shader stringblocks
                        for (var j = 0; j < shaderContentCount; j++)
                        {
                            var shaderLengthInclHeader = chunkReader.ReadInt32();
                            var unk = chunkReader.ReadUInt32(); //type?
                            b_.AppendLine(unk.ToString());
                            var shaderContentLength = chunkReader.ReadInt32();
                            var shaderContent = chunkReader.ReadChars(shaderContentLength);

                            // File.WriteAllText(Path.Combine(@"D:\shaders\PCGL DotA Core\processed spritecard", "shader_out_" + i + "_" + j + ".txt"), new string(shaderContent));
                            var shaderContentChecksum = chunkReader.ReadBytes(16);
                        }

                        // Reached end of shader content, skip remaining file length
                        chunkReader.ReadBytes((int)chunkReader.BaseStream.Length - (int)chunkReader.BaseStream.Position);
                    }
                }
            }
        }

        byte[] ReadShaderChunk(BinaryReader r, StringBuilder b_, int offset)
        {
            var prevPos = r.BaseStream.Position;
            r.BaseStream.Position = offset;
            var chunkSize = r.ReadUInt32();

            if (r.ReadUInt32() != 0x414D5A4C) throw new InvalidDataException("Not LZMA?");

            var uncompressedSize = r.ReadUInt32();
            var compressedSize = r.ReadUInt32();

            b_.AppendLine($"Chunk size: {chunkSize}");
            b_.AppendLine($"Compressed size: {compressedSize}");
            b_.AppendLine($"Uncompressed size: {uncompressedSize} ({(uncompressedSize - compressedSize) / (double)uncompressedSize:P2} compression)");

            var decoder = new Decoder();
            decoder.SetDecoderProperties(r.ReadBytes(5));

            var compressedBuffer = r.ReadBytes((int)compressedSize);

            r.BaseStream.Position = prevPos;

            using var inputStream = new MemoryStream(compressedBuffer);
            using var outStream = new MemoryStream((int)uncompressedSize);
            decoder.Code(inputStream, outStream, compressedBuffer.Length, uncompressedSize, null);
            return outStream.ToArray();
        }
    }

    #endregion

    #region HammerEntities
    //was:Utils/HammerEntities

    public static class HammerEntities
    {
        public static string GetToolModel(string classname)
            => classname switch
            {
                "ai_attached_item_manager" => "materials/editor/info_target.vmat",
                "ai_battle_line" => "models/pigeon.vmdl",
                "ai_goal_fightfromcover" => "materials/editor/ai_goal_follow.vmat",
                "ai_goal_follow" => "materials/editor/ai_goal_follow.vmat",
                "ai_goal_injured_follow" => "materials/editor/ai_goal_follow.vmat",
                "ai_goal_lead" => "materials/editor/ai_goal_lead.vmat",
                "ai_goal_lead_weapon" => "materials/editor/ai_goal_lead.vmat",
                "ai_goal_police" => "materials/editor/ai_goal_police.vmat",
                "ai_goal_standoff" => "materials/editor/ai_goal_standoff.vmat",
                "ai_relationship" => "materials/editor/ai_relationship.vmat",
                "ai_scripted_abilityusage" => "materials/editor/aiscripted_schedule.vmat",
                "ai_scripted_idle" => "materials/editor/aiscripted_schedule.vmat",
                "ai_scripted_moveto" => "materials/editor/ai_scripted_moveto.vmat",
                "ai_sound" => "materials/editor/ai_sound.vmat",
                "aiscripted_schedule" => "materials/editor/aiscripted_schedule.vmat",
                "ambient_generic" => "materials/editor/ambient_generic.vmat",
                "assault_assaultpoint" => "materials/editor/assault_point.vmat",
                "assault_rallypoint" => "materials/editor/assault_rally.vmat",
                "beam_spotlight" => "models/editor/cone_helper.vmdl",
                "color_correction" => "materials/editor/color_correction.vmat",
                "combine_attached_armor_prop" => "models/characters/combine_soldier_heavy/combine_hand_shield.vmdl",
                "combine_mine" => "models/props_combine/combine_mine/combine_mine.vmdl",
                "commentary_auto" => "materials/editor/commentary_auto.vmat",
                "devtest_hierarchy2" => "models/survivors/survivor_gambler.vmdl",
                "dota_color_correction" => "materials/editor/color_correction.vmat",
                "dota_world_particle_system" => "models/editor/cone_helper.vmdl",
                "env_clock" => "materials/editor/logic_timer.vmat",
                "env_combined_light_probe_volume" => "models/editor/env_cubemap.vmdl",
                "env_cubemap" => "models/editor/env_cubemap.vmdl",
                "env_cubemap_box" => "models/editor/env_cubemap.vmdl",
                "env_cubemap_fog" => "materials/editor/env_cubemap_fog.vmat",
                "env_decal" => "models/editor/axis_helper_thick.vmdl",
                "env_deferred_spot_light" => "models/editor/cone_helper.vmdl",
                "env_dof_controller" => "materials/editor/env_dof_controller.vmat",
                "env_explosion" => "materials/editor/env_explosion.vmat",
                "env_fade" => "materials/editor/env_fade.vmat",
                "env_fire" => "materials/editor/env_fire.vmat",
                "env_firesource" => "materials/editor/env_firesource.vmat",
                "env_fog_controller" => "materials/editor/env_fog_controller.vmat",
                "env_global_light" => "models/editor/spot.vmdl",
                "env_gradient_fog" => "materials/editor/env_fog_controller.vmat",
                "env_headcrabcanister" => "models/props_combine/headcrabcannister01b.vmdl",
                "env_instructor_hint" => "materials/editor/env_instructor_hint.vmat",
                "env_instructor_vr_hint" => "materials/editor/env_instructor_hint.vmat",
                "env_light_probe_volume" => "models/editor/iv_helper.vmdl",
                "env_lightglow" => "models/editor/axis_helper_thick.vmdl",
                "env_microphone" => "materials/editor/env_microphone.vmat",
                "env_physexplosion" => "materials/editor/env_physexplosion.vmat",
                "env_physimpact" => "materials/editor/env_physexplosion.vmat",
                "env_projectedtexture" => "models/editor/spot.vmdl",
                "env_rotorshooter" => "materials/editor/env_shooter.vmat",
                "env_shake" => "materials/editor/env_shake.vmat",
                "env_shooter" => "materials/editor/env_shooter.vmat",
                "env_sky" => "materials/editor/env_sky.vmat",
                "env_soundscape" => "materials/editor/env_soundscape.vmat",
                "env_soundscape_proxy" => "materials/editor/env_soundscape.vmat",
                "env_soundscape_triggerable" => "materials/editor/env_soundscape.vmat",
                "env_spark" => "materials/editor/env_spark.vmat",
                "env_speaker" => "materials/editor/ambient_generic.vmat",
                "env_spherical_vignette" => "materials/editor/env_fog_controller.vmat",
                "env_sun" => "models/editor/light_environment.vmdl",
                "env_texturetoggle" => "materials/editor/env_texturetoggle.vmat",
                "env_tilt" => "models/editor/axis_helper_thick.vmdl",
                "env_time_of_day" => "models/editor/sky_helper.vmdl",
                "env_tonemap_controller" => "materials/editor/env_tonemap_controller.vmat",
                "env_volumetric_fog_volume" => "materials/editor/fog_volume.vmat",
                "env_wind" => "materials/editor/env_wind.vmat",
                "env_wind_clientside" => "materials/editor/env_wind.vmat",
                "env_world_lighting" => "materials/editor/light_env.vmat",
                "filter_activator_attribute_int" => "materials/editor/filter_class.vmat",
                "filter_activator_class" => "materials/editor/filter_class.vmat",
                "filter_activator_context" => "materials/editor/filter_name.vmat",
                "filter_activator_mass_greater" => "materials/editor/filter_class.vmat",
                "filter_activator_model" => "materials/editor/filter_name.vmat",
                "filter_activator_name" => "materials/editor/filter_name.vmat",
                "filter_combineball_type" => "materials/editor/filter_class.vmat",
                "filter_damage_type" => "materials/editor/filter_type.vmat",
                "filter_enemy" => "materials/editor/filter_class.vmat",
                "filter_los" => "materials/editor/filter_class.vmat",
                "filter_multi" => "materials/editor/filter_multiple.vmat",
                "filter_proximity" => "materials/editor/filter_class.vmat",
                "filter_vr_grenade" => "materials/editor/filter_name.vmat",
                "game_end" => "materials/editor/game_end.vmat",
                "game_text" => "materials/editor/game_text.vmat",
                "ghost_actor" => "materials/editor/ghost_actor.vmat",
                "ghost_speaker" => "materials/editor/ghost_speaker.vmat",
                "gibshooter" => "materials/editor/gibshooter.vmat",
                "grenade_helicopter" => "models/combine_helicopter/helicopter_bomb01.vmdl",
                "haptic_relay" => "materials/editor/haptic_relay.vmat",
                "hlvr_weapon_energygun" => "models/weapons/vr_alyxgun/vr_alyxgun.vmdl",
                "info_cull_triangles" => "materials/editor/info_cull_triangles.vmat",
                "info_hint" => "models/editor/node_hint.vmdl",
                "info_hlvr_equip_player" => "materials/editor/info_hlvr_equip_player.vmat",
                "info_hlvr_holo_hacking_plug" => "models/props_combine/combine_doors/combine_hacking_interact_point.vmdl",
                "info_hlvr_offscreen_particle_texture" => "materials/editor/info_hlvr_offscreen_particle_texture.vmat",
                "info_hlvr_toner_path" => "materials/editor/info_hlvr_toner_path.vmat",
                "info_landmark" => "materials/editor/info_landmark.vmat",
                "info_lighting" => "materials/editor/info_lighting.vmat",
                "info_node" => "models/editor/ground_node.vmdl",
                "info_node_air" => "models/editor/air_node.vmdl",
                "info_node_air_hint" => "models/editor/air_node_hint.vmdl",
                "info_node_climb" => "models/editor/climb_node.vmdl",
                "info_node_hint" => "models/editor/ground_node_hint.vmdl",
                "info_notepad" => "materials/editor/info_notepad.vmat",
                "info_npc_spawn_destination" => "materials/editor/info_target.vmat",
                "info_overlay" => "models/editor/overlay_helper.vmdl",
                "info_particle_system" => "models/editor/cone_helper.vmdl",
                "info_particle_target" => "models/editor/cone_helper.vmdl",
                "info_player_start" => "models/editor/playerstart.vmdl",
                "info_player_start_badguys" => "models/editor/playerstart.vmdl",
                "info_player_start_dota" => "models/editor/playerstart.vmdl",
                "info_player_start_goodguys" => "models/editor/playerstart.vmdl",
                "info_projecteddecal" => "models/editor/axis_helper_thick.vmdl",
                "info_radar_target" => "materials/editor/info_target.vmat",
                "info_roquelaire_perch" => "materials/editor/info_target.vmat",
                "info_snipertarget" => "materials/editor/info_target.vmat",
                "info_spawngroup_landmark" => "materials/editor/info_target.vmat",
                "info_spawngroup_load_unload" => "materials/editor/info_target.vmat",
                "info_target" => "materials/editor/info_target.vmat",
                "info_target_advisor_roaming_crash" => "materials/editor/info_target.vmat",
                "info_target_gunshipcrash" => "materials/editor/info_target.vmat",
                "info_target_helicopter_crash" => "materials/editor/info_target.vmat",
                "info_target_vehicle_transition" => "materials/editor/info_target.vmat",
                "info_teleport_destination" => "models/editor/playerstart.vmdl",
                "info_teleporter_countdown" => "materials/editor/info_target.vmat",
                "info_visibility_box" => "materials/editor/info_visibility_box.vmat",
                "info_world_layer" => "materials/editor/info_world_layer.vmat",
                "infodecal" => "models/editor/axis_helper_thick.vmdl",
                "item_ammo_357" => "models/items/357ammo.vmdl",
                "item_ammo_357_large" => "models/items/357ammobox.vmdl",
                "item_ammo_ar2" => "models/items/combine_rifle_cartridge01.vmdl",
                "item_ammo_ar2_altfire" => "models/items/combine_rifle_ammo01.vmdl",
                "item_ammo_ar2_large" => "models/items/combine_rifle_cartridge01.vmdl",
                "item_ammo_crate" => "models/items/ammocrate_rockets.vmdl",
                "item_ammo_crossbow" => "models/items/crossbowrounds.vmdl",
                "item_ammo_pistol" => "models/items/boxsrounds.vmdl",
                "item_ammo_pistol_large" => "models/items/boxsrounds.vmdl",
                "item_ammo_smg1" => "models/items/boxmrounds.vmdl",
                "item_ammo_smg1_grenade" => "models/items/ar2_grenade.vmdl",
                "item_ammo_smg1_large" => "models/items/boxmrounds.vmdl",
                "item_battery" => "models/items/battery.vmdl",
                "item_box_buckshot" => "models/items/boxbuckshot.vmdl",
                "item_dynamic_resupply" => "models/items/healthkit.vmdl",
                "item_healthcharger_DEPRECATED" => "models/props_combine/health_charger001.vmdl",
                "item_healthkit" => "models/items/healthkit.vmdl",
                "item_healthvial_DEPRECATED" => "models/items/healthvial/healthvial.vmdl",
                "item_hlvr_combine_console_tank" => "models/props_combine/combine_consoles/glass_tank_01.vmdl",
                "item_hlvr_grenade_xen" => "models/weapons/vr_xen_grenade/vr_xen_grenade.vmdl",
                "item_hlvr_headcrab_gland" => "models/props/headcrab_guts/headcrab_gland.vmdl",
                "item_hlvr_weapon_energygun" => "models/weapons/vr_alyxgun/vr_alyxgun.vmdl",
                "item_hlvr_weapon_generic_pistol" => "models/weapons/vr_alyxgun/vr_alyxgun.vmdl",
                "item_hlvr_weapon_rapidfire" => "models/weapons/vr_ipistol/vr_ipistol.vmdl",
                "item_hlvr_weapon_shotgun" => "models/weapons/vr_shotgun/vr_shotgun_b.vmdl",
                "item_rpg_round" => "models/weapons/w_missile_closed.vmdl",
                "item_suit" => "models/items/hevsuit.vmdl",
                "item_suitcharger" => "models/props_combine/suit_charger001.vmdl",
                "keyframe_rope" => "models/editor/axis_helper_thick.vmdl",
                "light_ambient" => "materials/editor/light_ambient.vmat",
                "light_ambientocclusion" => "materials/editor/light_ambientocclusion.vmat",
                "light_barn" => "models/editor/spot.vmdl",
                "light_dynamic" => "materials/editor/light.vmat",
                "light_environment" => "models/editor/sun.vmdl",
                "light_importance_volume" => "materials/editor/light_importance_volume.vmat",
                "light_irradvolume" => "models/editor/iv_helper.vmdl",
                "light_omni" => "models/editor/spot.vmdl",
                "light_ortho" => "models/editor/spot.vmdl",
                "light_rect" => "models/editor/spot.vmdl",
                "light_sky" => "materials/editor/light_sky.vmat",
                "light_spot" => "models/editor/spot.vmdl",
                "logic_achievement" => "materials/editor/logic_achievement.vmat",
                "logic_auto" => "materials/editor/logic_auto.vmat",
                "logic_autosave" => "materials/editor/logic_autosave.vmat",
                "logic_branch" => "materials/editor/logic_branch.vmat",
                "logic_case" => "materials/editor/logic_case.vmat",
                "logic_choreographed_scene" => "materials/editor/choreo_scene.vmat",
                "logic_compare" => "materials/editor/logic_compare.vmat",
                "logic_distance_autosave" => "materials/editor/logic_autosave.vmat",
                "logic_door_barricade" => "materials/editor/logic_door_barricade.vmat",
                "logic_gameevent_listener" => "materials/editor/game_event_listener.vmat",
                "logic_handsup_listener" => "materials/editor/logic_hands_up.vmat",
                "logic_multicompare" => "materials/editor/logic_multicompare.vmat",
                "logic_npc_counter_aabb" => "materials/editor/math_counter.vmat",
                "logic_npc_counter_obb" => "materials/editor/math_counter.vmat",
                "logic_npc_counter_radius" => "materials/editor/math_counter.vmat",
                "logic_random_outputs" => "materials/editor/logic_random_outputs.vmat",
                "logic_relay" => "materials/editor/logic_relay.vmat",
                "logic_scene_list_manager" => "materials/editor/choreo_manager.vmat",
                "logic_script" => "materials/editor/logic_script.vmat",
                "logic_scripted_scenario" => "materials/editor/choreo_scene.vmat",
                "logic_timer" => "materials/editor/logic_timer.vmat",
                "math_counter" => "materials/editor/math_counter.vmat",
                "move_rope" => "models/editor/axis_helper.vmdl",
                "npc_antlion_grub" => "models/antlion_grub.vmdl",
                "npc_apcdriver" => "models/roller.vmdl",
                "npc_barney" => "models/characters/barney/barney.vmdl",
                "npc_bullseye" => "materials/editor/bullseye.vmat",
                "npc_clawscanner" => "models/shield_scanner.vmdl",
                "npc_combine_advisor_roaming" => "models/advisor.vmdl",
                "npc_combine_camera" => "models/combine_camera/combine_camera.vmdl",
                "npc_combine_cannon" => "models/combine_soldier.vmdl",
                "npc_combinedropship" => "models/combine_dropship.vmdl",
                "npc_combinegunship" => "models/creatures/gunship/gunship.vmdl",
                "npc_crabsynth" => "models/Synth.vmdl",
                "npc_cranedriver" => "models/roller.vmdl",
                "npc_crow" => "models/creatures/crow/crow.vmdl",
                "npc_cscanner" => "models/creatures/scanner/combine_scanner.vmdl",
                "npc_dog" => "models/dog.vmdl",
                "npc_enemyfinder" => "materials/editor/enemyfinder.vmat",
                "npc_fastzombie" => "models/Zombie/fast.vmdl",
                "npc_fastzombie_torso" => "models/Zombie/Fast_torso.vmdl",
                "npc_fisherman" => "models/Barney.vmdl",
                "npc_gman" => "models/gman.vmdl",
                "npc_grenade_frag" => "models/Weapons/w_grenade.vmdl",
                "npc_headcrab_armored" => "models/creatures/headcrab_armored/headcrab_armored.vmdl",
                "npc_heli_avoidsphere" => "materials/editor/env_firesource.vmat",
                "npc_helicopter" => "models/creatures/combine_helicopter/combine_helicopter.vmdl",
                "npc_hunter" => "models/hunter.vmdl",
                "npc_hunter_maker" => "materials/editor/npc_maker.vmat",
                "npc_ichthyosaur" => "models/ichthyosaur.vmdl",
                "npc_launcher" => "models/junk/w_traffcone.vmdl",
                "npc_maker" => "materials/editor/npc_maker.vmat",
                "npc_manhack" => "models/creatures/manhack/manhack.vmdl",
                "npc_metropolice" => "models/characters/metrocop/metrocop.vmdl",
                "npc_missiledefense" => "models/missile_defense.vmdl",
                "npc_monk" => "models/Monk.vmdl",
                "npc_mortarsynth" => "models/mortarsynth.vmdl",
                "npc_mossman" => "models/mossman.vmdl",
                "npc_pigeon" => "models/creatures/pigeon/pigeon.vmdl",
                "npc_poisonzombie" => "models/Zombie/Poison.vmdl",
                "npc_rollermine" => "models/roller.vmdl",
                "npc_seagull" => "models/creatures/seagull/seagull.vmdl",
                "npc_sniper" => "models/combine_soldier.vmdl",
                "npc_stalker" => "models/Stalker.vmdl",
                "npc_strider" => "models/creatures/strider/combine_strider.vmdl",
                "npc_template_maker" => "materials/editor/npc_maker.vmat",
                "npc_turret_citizen" => "models/props/citizen_battery_turret.vmdl",
                "npc_turret_floor" => "models/combine_turrets/floor_turret.vmdl",
                "npc_turret_ground" => "models/combine_turrets/ground_turret.vmdl",
                "npc_vehicledriver" => "models/roller.vmdl",
                "npc_zombie_blind" => "models/creatures/zombie_blind/zombie_blind.vmdl",
                "npc_zombie_torso" => "models/creatures/zombie_classic/zombie_classic_torso.vmdl",
                "npc_zombine" => "models/Zombie/zombie_soldier.vmdl",
                "phys_ballsocket" => "materials/editor/phys_ballsocket.vmat",
                "phys_constraint" => "models/editor/axis_helper_thick.vmdl",
                "phys_genericconstraint" => "models/editor/axis_helper.vmdl",
                "phys_hinge" => "models/editor/axis_helper.vmdl",
                "phys_hinge_local" => "models/editor/axis_helper.vmdl",
                "phys_lengthconstraint" => "models/editor/axis_helper.vmdl",
                "phys_pulleyconstraint" => "models/editor/axis_helper.vmdl",
                "phys_ragdollconstraint" => "models/editor/axis_helper.vmdl",
                "phys_ragdollmagnet" => "materials/editor/info_target.vmat",
                "phys_slideconstraint" => "models/editor/axis_helper.vmdl",
                "phys_splineconstraint" => "models/editor/axis_helper_thick.vmdl",
                "phys_spring" => "models/editor/axis_helper.vmdl",
                "point_aimat" => "models/editor/point_aimat.vmdl",
                "point_camera" => "models/editor/camera.vmdl",
                "point_camera_vertical_fov" => "models/editor/camera.vmdl",
                "point_devshot_camera" => "models/editor/camera.vmdl",
                "point_grabbable" => "materials/editor/point_grabbable.vmat",
                "point_hlvr_player_input_modifier" => "materials/editor/point_hlvr_player_input_modifier.vmat",
                "point_hlvr_strip_player" => "materials/editor/point_hlvr_strip_player.vmat",
                "point_instructor_event" => "materials/editor/env_instructor_hint.vmat",
                "point_soundevent" => "materials/editor/ambient_generic.vmat",
                "point_spotlight" => "models/editor/cone_helper.vmdl",
                "point_teleport" => "models/editor/axis_helper_thick.vmdl",
                "point_template" => "materials/editor/point_template.vmat",
                "point_value_remapper" => "models/editor/axis_helper_thick.vmdl",
                "point_viewcontrol" => "models/editor/camera.vmdl",
                "point_workplane" => "models/editor/axis_helper_thick.vmdl",
                "point_worldtext" => "models/editor/axis_helper_thick.vmdl",
                "postprocess_controller" => "materials/editor/postprocess_controller.vmat",
                "prop_coreball" => "models/props_combine/coreball.vmdl",
                "prop_reviver_heart" => "models/creatures/headcrab_reviver/reviver_heart.vmdl",
                "save_photogrammetry_anchor" => "materials/editor/save_photogrammetry_anchor.vmat",
                "scripted_sentence" => "materials/editor/scripted_sentence.vmat",
                "scripted_sequence" => "models/editor/scripted_sequence.vmdl",
                "scripted_target" => "materials/editor/info_target.vmat",
                "shadow_control" => "materials/editor/shadow_control.vmat",
                "snd_event_alignedbox" => "materials/editor/snd_event.vmat",
                "snd_event_param" => "materials/editor/snd_opvar_set.vmat",
                "snd_event_path_corner" => "materials/editor/snd_event.vmat",
                "snd_event_point" => "materials/editor/snd_event.vmat",
                "snd_opvar_set" => "materials/editor/snd_opvar_set.vmat",
                "snd_opvar_set_aabb" => "materials/editor/snd_opvar_set.vmat",
                "snd_opvar_set_obb" => "materials/editor/snd_opvar_set.vmat",
                "snd_opvar_set_path_corner" => "materials/editor/snd_opvar_set.vmat",
                "snd_opvar_set_point" => "materials/editor/snd_opvar_set.vmat",
                "snd_opvar_set_wind_obb" => "materials/editor/snd_opvar_set.vmat",
                "snd_sound_area_obb" => "materials/editor/snd_opvar_set.vmat",
                "snd_sound_area_sphere" => "materials/editor/snd_opvar_set.vmat",
                "snd_soundscape" => "materials/editor/env_soundscape.vmat",
                "snd_soundscape_proxy" => "materials/editor/env_soundscape.vmat",
                "snd_soundscape_triggerable" => "materials/editor/env_soundscape.vmat",
                "snd_stack_save" => "materials/editor/snd_event.vmat",
                "sound_opvar_set" => "materials/editor/ambient_generic.vmat",
                "tanktrain_ai" => "materials/editor/tanktrain_ai.vmat",
                "tanktrain_aitarget" => "materials/editor/tanktrain_aitarget.vmat",
                "tutorial_npc_blocker" => "materials/editor/info_target.vmat",
                "vgui_movie_display" => "models/editor/axis_helper_thick.vmdl",
                "vgui_slideshow_display" => "models/editor/axis_helper_thick.vmdl",
                "visibility_hint" => "materials/editor/visibility_hint.vmat",
                "vr_teleport_marker" => "models/effects/teleport/teleport_marker.vmdl",
                "water_lod_control" => "materials/editor/waterlodcontrol.vmat",
                "weapon_357" => "models/weapons/w_357.vmdl",
                "weapon_alyxgun" => "models/weapons/W_Alyx_Gun.vmdl",
                "weapon_annabelle" => "models/weapons/W_annabelle.vmdl",
                "weapon_ar2" => "models/weapons/vr_irifle/vr_irifle.vmdl",
                "weapon_bugbait" => "models/spore.vmdl",
                "weapon_crossbow" => "models/weapons/w_crossbow.vmdl",
                "weapon_crowbar" => "models/weapons/w_crowbar.vmdl",
                "weapon_frag" => "models/weapons/w_grenade.vmdl",
                "weapon_physcannon" => "models/weapons/w_physics.vmdl",
                "weapon_pistol" => "models/weapons/vr_pistol/vr_pistol.vmdl",
                "weapon_rpg" => "models/weapons/w_rocket_launcher.vmdl",
                "weapon_shotgun" => "models/weapons/vr_shotgun/vr_shotgun.vmdl",
                "weapon_smg1" => "models/weaponsmodels/weapons/vr_smg/vr_smg.vmdl",
                "weapon_striderbuster" => "models/magnusson_device.vmdl",
                "weapon_stunstick" => "models/weapons/vr_baton/vr_baton.vmdl",
                "weapon_zipline" => "models/weapons/w_crossbow.vmdl",
                "xen_flora_animatedmover" => "models/props/xen_infestation_v2/xen_v2_floater_jellybobber.vmdl",
                "xen_foliage_bloater" => "models/props/xen_infestation/boomerplant_01.vmdl",
                "xen_foliage_grenade_spawner" => "models/props/xen_infestation/xen_grenade_plant.vmdl",
                "xen_foliage_turret" => "models/props/xen_infestation/xen_grenade_plant.vmdl",
                _ => "materials/editor/obsolete.vmat",
            };
    }

    #endregion

    #region ToolsAssetInfo

    public class ToolsAssetInfo : IHaveMetaInfo
    {
        public const uint MAGIC = 0xC4CCACE8;
        public const uint MAGIC2 = 0xC4CCACE9;
        public const uint GUARD = 0x049A48B2;

        public ToolsAssetInfo() { }
        public ToolsAssetInfo(BinaryReader r) => Read(r);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Text", Name = "Text", Value = ToString() }),
            new("ToolsAssetInfo", items: [
                new($"Mods: {Mods.Count}"),
                new($"Directories: {Directories.Count}"),
                new($"Filenames: {Filenames.Count}"),
                new($"Extensions: {Extensions.Count}"),
                new($"EditInfoKeys: {EditInfoKeys.Count}"),
                new($"MiscStrings: {MiscStrings.Count}"),
                new($"ConstructedFilepaths: {ConstructedFilepaths.Count}"),
            ]),
        ];

        public readonly List<string> Mods = [];
        public readonly List<string> Directories = [];
        public readonly List<string> Filenames = [];
        public readonly List<string> Extensions = [];
        public readonly List<string> EditInfoKeys = [];
        public readonly List<string> MiscStrings = [];
        public readonly List<string> ConstructedFilepaths = [];
        public readonly List<string> UnknownSoundField1 = [];
        public readonly List<string> UnknownSoundField2 = [];

        public void Read(BinaryReader r)
        {
            var magic = r.ReadUInt32();
            if (magic != MAGIC && magic != MAGIC2) throw new InvalidDataException("Given file is not tools_asset_info.");

            var version = r.ReadUInt32();
            if (version < 9 || version > 13) throw new InvalidDataException($"Unsupported version: {version}");
            var fileCount = r.ReadUInt32();
            if (r.ReadUInt32() != 1) throw new InvalidDataException($"Invalid blockId");

            ReadStringsBlock(r, Mods);
            ReadStringsBlock(r, Directories);
            ReadStringsBlock(r, Filenames);
            ReadStringsBlock(r, Extensions);
            ReadStringsBlock(r, EditInfoKeys);
            ReadStringsBlock(r, MiscStrings);
            if (version >= 12)
            {
                ReadStringsBlock(r, UnknownSoundField1);
                ReadStringsBlock(r, UnknownSoundField2);
            }

            for (var i = 0; i < fileCount; i++)
            {
                var hash = r.ReadUInt64();
                var unk1 = (int)(hash >> 61) & 7;
                var addonIndex = (int)(hash >> 52) & 0x1FF;
                var directoryIndex = (int)(hash >> 33) & 0x7FFFF;
                var filenameIndex = (int)(hash >> 10) & 0x7FFFFF;
                var extensionIndex = (int)(hash & 0x3FF);
                //Console.WriteLine($"{unk1} {addonIndex} {directoryIndex} {filenameIndex} {extensionIndex}");
                var path = new StringBuilder();
                if (addonIndex != 0x1FF) { path.Append(Mods[addonIndex]); path.Append('/'); }
                if (directoryIndex != 0x7FFFF) { path.Append(Directories[directoryIndex]); path.Append('/'); }
                if (filenameIndex != 0x7FFFFF) { path.Append(Filenames[filenameIndex]); }
                if (extensionIndex != 0x3FF) { path.Append('.'); path.Append(Extensions[extensionIndex]); }
                ConstructedFilepaths.Add(path.ToString());
            }
        }

        static void ReadStringsBlock(BinaryReader r, ICollection<string> output)
        {
            var count = r.ReadUInt32();
            for (var i = 0U; i < count; i++) output.Add(r.ReadVUString());
        }

        public override string ToString()
        {
            var b = new StringBuilder();
            foreach (var str in ConstructedFilepaths) b.AppendLine(str);
            return b.ToString();
        }
    }

    #endregion

    #region ValveFont

    public class ValveFont
    {
        const string MAGIC = "VFONT1";
        const byte MAGICTRICK = 167;

        public byte[] Read(BinaryReader r)
        {
            // Magic is at the end
            r.BaseStream.Seek(-MAGIC.Length, SeekOrigin.End);
            if (Encoding.ASCII.GetString(r.ReadBytes(MAGIC.Length)) != MAGIC) throw new InvalidDataException("Given file is not a vfont, version 1.");
            r.End(-1 - MAGIC.Length);

            // How many magic bytes there are
            var bytes = r.ReadByte();
            var output = new byte[r.BaseStream.Length - MAGIC.Length - bytes];
            var magic = (int)MAGICTRICK;

            // Read the magic bytes
            r.Skip(-bytes);
            bytes--;
            for (var i = 0; i < bytes; i++) magic ^= (r.ReadByte() + MAGICTRICK) % 256;

            // Decode the rest
            r.Seek(0);
            for (var i = 0; i < output.Length; i++)
            {
                var currentByte = r.ReadByte();
                output[i] = (byte)(currentByte ^ magic);
                magic = (currentByte + MAGICTRICK) % 256;
            }
            return output;
        }
    }

    #endregion
}
