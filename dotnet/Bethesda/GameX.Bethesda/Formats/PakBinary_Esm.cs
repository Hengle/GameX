﻿using GameX.Formats;
using GameX.Bethesda.Formats.Records;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

// TES3
//http://en.uesp.net/wiki/Bethesda3Mod:File_Format
//https://github.com/TES5Edit/TES5Edit/blob/dev/wbDefinitionsTES3.pas
//http://en.uesp.net/morrow/tech/mw_esm.txt
//https://github.com/mlox/mlox/blob/master/util/tes3cmd/tes3cmd
// TES4
//https://github.com/WrinklyNinja/esplugin/tree/master/src
//http://en.uesp.net/wiki/Bethesda4Mod:Mod_File_Format
//https://github.com/TES5Edit/TES5Edit/blob/dev/wbDefinitionsTES4.pas 
// TES5
//http://en.uesp.net/wiki/Bethesda5Mod:Mod_File_Format
//https://github.com/TES5Edit/TES5Edit/blob/dev/wbDefinitionsTES5.pas 

namespace GameX.Bethesda.Formats
{
    /// <summary>
    /// PakBinaryBethesdaEsm
    /// </summary>
    /// <seealso cref="GameX.Formats._Packages.PakBinaryBethesdaEsm" />
    public unsafe class PakBinary_Esm : PakBinary
    {
        public static readonly PakBinary Instance = new PakBinary_Esm();

        const int RecordHeaderSizeInBytes = 16;
        public FormFormat Format;
        public Dictionary<FormType, RecordGroup> Groups;

        static FormFormat GetFormat(string game)
            => game switch
            {
                // tes
                "Morrowind" => FormFormat.TES3,
                "Oblivion" => FormFormat.TES4,
                var x when x == "Skyrim" || x == "SkyrimSE" || x == "SkyrimVR" => FormFormat.TES5,
                // fallout
                var x when x == "Fallout3" || x == "FalloutNV" => FormFormat.TES4,
                var x when x == "Fallout4" || x == "Fallout4VR" => FormFormat.TES5,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game),
            };

        /// <summary>
        /// Reads the asynchronous.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="r">The r.</param>
        /// <param name="stage">The stage.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">stage</exception>
        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            Format = GetFormat(source.Game.Id);
            var recordLevel = 1;
            var filePath = source.PakPath;
            var poolAction = (GenericPoolAction<BinaryReader>)source.GetReader().Action; //: Leak
            var rootHeader = new Header(r, Format, null);
            if ((Format == FormFormat.TES3 && rootHeader.Type != FormType.TES3) || (Format != FormFormat.TES3 && rootHeader.Type != FormType.TES4)) throw new FormatException($"{filePath} record header {rootHeader.Type} is not valid for this {Format}");
            var rootRecord = rootHeader.CreateRecord(rootHeader.Position, recordLevel);
            rootRecord.Read(r, filePath, Format);
            // morrowind hack
            if (Format == FormFormat.TES3)
            {
                var group = new RecordGroup(poolAction, filePath, Format, recordLevel);
                group.AddHeader(new Header
                {
                    Label = 0,
                    DataSize = (uint)(r.BaseStream.Length - r.Tell()),
                    Position = r.Tell(),
                });
                group.Load();
                Groups = group.Records.GroupBy(x => x.Header.Type)
                    .ToDictionary(x => x.Key, x =>
                    {
                        var s = new RecordGroup(null, filePath, Format, recordLevel) { Records = x.ToList() };
                        s.AddHeader(new Header { Label = x.Key }, load: false);
                        return s;
                    });
                return Task.CompletedTask;
            }
            // read groups
            Groups = [];
            var endPosition = r.BaseStream.Length;
            while (r.BaseStream.Position < endPosition)
            {
                var header = new Header(r, Format, null);
                if (header.Type != FormType.GRUP) throw new InvalidOperationException($"{header.Type} not GRUP");
                var nextPosition = r.Tell() + header.DataSize;
                if (!Groups.TryGetValue(header.Label, out var group)) { group = new RecordGroup(poolAction, filePath, Format, recordLevel); Groups.Add(header.Label, group); }
                group.AddHeader(header);
                r.Seek(nextPosition);
            }
            return Task.CompletedTask;
        }

        // TES3
        Dictionary<string, IRecord> MANYsById;
        Dictionary<long, LTEXRecord> LTEXsById;
        Dictionary<Int3, LANDRecord> LANDsById;
        Dictionary<Int3, CELLRecord> CELLsById;
        Dictionary<string, CELLRecord> CELLsByName;

        // TES4
        Dictionary<uint, Tuple<WRLDRecord, RecordGroup[]>> WRLDsById;
        Dictionary<string, LTEXRecord> LTEXsByEid;

        public override Task Process(BinaryPakFile source)
        {
            if (Format == FormFormat.TES3)
            {
                var statGroups = new List<Record>[] { Groups.ContainsKey(FormType.STAT) ? Groups[FormType.STAT].Load() : null };
                MANYsById = statGroups.SelectMany(x => x).Where(x => x != null).ToDictionary(x => x.EDID.Value, x => (IRecord)x);
                LTEXsById = Groups[FormType.LTEX].Load().Cast<LTEXRecord>().ToDictionary(x => x.INTV.Value);
                var lands = Groups[FormType.LAND].Load().Cast<LANDRecord>().ToList();
                foreach (var land in lands) land.GridId = new Int3(land.INTV.CellX, land.INTV.CellY, 0);
                LANDsById = lands.ToDictionary(x => x.GridId);
                var cells = Groups[FormType.CELL].Load().Cast<CELLRecord>().ToList();
                foreach (var cell in cells) cell.GridId = new Int3(cell.XCLC.Value.GridX, cell.XCLC.Value.GridY, !cell.IsInterior ? 0 : -1);
                CELLsById = cells.Where(x => !x.IsInterior).ToDictionary(x => x.GridId);
                CELLsByName = cells.Where(x => x.IsInterior).ToDictionary(x => x.EDID.Value);
                return Task.CompletedTask;
            }
            var wrldsByLabel = Groups[FormType.WRLD].GroupsByLabel;
            WRLDsById = Groups[FormType.WRLD].Load().Cast<WRLDRecord>().ToDictionary(x => x.Id, x => { wrldsByLabel.TryGetValue(x.Id, out var wrlds); return new Tuple<WRLDRecord, RecordGroup[]>(x, wrlds); });
            LTEXsByEid = Groups[FormType.LTEX].Load().Cast<LTEXRecord>().ToDictionary(x => x.EDID.Value);
            return Task.CompletedTask;
        }
    }
}