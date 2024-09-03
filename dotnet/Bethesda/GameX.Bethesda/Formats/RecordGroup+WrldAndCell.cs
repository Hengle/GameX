using GameX.Bethesda.Formats.Records;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace GameX.Bethesda.Formats
{
    partial class RecordGroup
    {
        internal HashSet<uint> _ensureCELLsByLabel;
        internal Dictionary<Int3, CELLRecord> CELLsById;
        internal Dictionary<Int3, LANDRecord> LANDsById;

        public RecordGroup[] EnsureWrldAndCell(Int3 cellId)
        {
            var cellBlockX = (short)(cellId.X >> 5);
            var cellBlockY = (short)(cellId.Y >> 5);
            var cellBlockIdx = new byte[4];
            Buffer.BlockCopy(BitConverter.GetBytes(cellBlockY), 0, cellBlockIdx, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(cellBlockX), 0, cellBlockIdx, 2, 2);
            Load();
            var cellBlockId = BitConverter.ToUInt32(cellBlockIdx);
            if (GroupsByLabel.TryGetValue(cellBlockId, out var cellBlocks))
                return cellBlocks.Select(x => x.EnsureCell(cellId)).ToArray();
            return null;
        }

        //= nxn[nbits] + 4x4[2bits] + 8x8[3bit]
        public RecordGroup EnsureCell(Int3 cellId)
        {
            _ensureCELLsByLabel ??= new HashSet<uint>();
            var cellBlockX = (short)(cellId.X >> 5);
            var cellBlockY = (short)(cellId.Y >> 5);
            var cellSubBlockX = (short)(cellId.X >> 3);
            var cellSubBlockY = (short)(cellId.Y >> 3);
            var cellSubBlockIdx = new byte[4];
            Buffer.BlockCopy(BitConverter.GetBytes(cellSubBlockY), 0, cellSubBlockIdx, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(cellSubBlockX), 0, cellSubBlockIdx, 2, 2);
            var cellSubBlockId = BitConverter.ToUInt32(cellSubBlockIdx);
            if (_ensureCELLsByLabel.Contains(cellSubBlockId)) return this;
            Load();
            CELLsById ??= [];
            LANDsById ??= cellId.Z >= 0 ? [] : null;
            if (GroupsByLabel.TryGetValue(cellSubBlockId, out var cellSubBlocks))
            {
                // find cell
                var cellSubBlock = cellSubBlocks.Single();
                cellSubBlock.Load(true);
                foreach (var cell in cellSubBlock.Records.Cast<CELLRecord>())
                {
                    cell.GridId = new Int3(cell.XCLC.Value.GridX, cell.XCLC.Value.GridY, !cell.IsInterior ? cellId.Z : -1);
                    CELLsById.Add(cell.GridId, cell);
                    // find children
                    if (cellSubBlock.GroupsByLabel.TryGetValue(cell.Id, out var cellChildren))
                    {
                        var cellChild = cellChildren.Single();
                        var cellTemporaryChildren = cellChild.Groups.Single(x => x.Headers.First().GroupType == Header.HeaderGroupType.CellTemporaryChildren);
                        foreach (var land in cellTemporaryChildren.Records.Cast<LANDRecord>())
                        {
                            land.GridId = new Int3(cell.XCLC.Value.GridX, cell.XCLC.Value.GridY, !cell.IsInterior ? cellId.Z : -1);
                            LANDsById.Add(land.GridId, land);
                        }
                    }
                }
                _ensureCELLsByLabel.Add(cellSubBlockId);
                return this;
            }
            return null;
        }
    }
}
