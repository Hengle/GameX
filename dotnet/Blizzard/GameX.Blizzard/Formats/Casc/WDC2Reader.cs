﻿using GameX.FileSystems.Casc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ext = GameX.FileSystems.Casc.CStringExtensions;

namespace GameX.Blizzard.Formats.Casc
{
    public class WDC2Row : IDB2Row
    {
        BitReader m_data;
        WDC2Reader m_reader;
        int m_dataOffset;
        int m_recordsOffset;
        int m_refId;
        int m_id;

        public int GetId() => m_id;
        public void SetId(int id) => m_id = id;

        FieldMetaData[] m_fieldMeta;
        ColumnMetaData[] m_columnMeta;
        Value32[][] m_palletData;
        Dictionary<int, Value32>[] m_commonData;
        Dictionary<long, string> m_stringsTable;

        public WDC2Row(WDC2Reader reader, BitReader data, int recordsOffset, int id, int refId, Dictionary<long, string> stringsTable)
        {
            m_reader = reader;
            m_data = data;
            m_recordsOffset = recordsOffset;
            m_refId = refId;

            m_dataOffset = m_data.Offset;

            m_fieldMeta = reader.Meta;
            m_columnMeta = reader.ColumnMeta;
            m_palletData = reader.PalletData;
            m_commonData = reader.CommonData;
            m_stringsTable = stringsTable;

            if (id != -1)
                m_id = id;
            else
            {
                int idFieldIndex = reader.IdFieldIndex;

                m_data.Position = m_columnMeta[idFieldIndex].RecordOffset;

                m_id = FieldReader.GetFieldValue<int>(0, m_data, m_fieldMeta[idFieldIndex], m_columnMeta[idFieldIndex], m_palletData[idFieldIndex], m_commonData[idFieldIndex]);
            }
        }

        static Dictionary<Type, Func<int, BitReader, int, FieldMetaData, ColumnMetaData, Value32[], Dictionary<int, Value32>, Dictionary<long, string>, object>> simpleReaders = new Dictionary<Type, Func<int, BitReader, int, FieldMetaData, ColumnMetaData, Value32[], Dictionary<int, Value32>, Dictionary<long, string>, object>>
        {
            [typeof(float)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => FieldReader.GetFieldValue<float>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(int)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => FieldReader.GetFieldValue<int>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(uint)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => FieldReader.GetFieldValue<uint>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(short)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => FieldReader.GetFieldValue<short>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(ushort)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => FieldReader.GetFieldValue<ushort>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(sbyte)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => FieldReader.GetFieldValue<sbyte>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(byte)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => FieldReader.GetFieldValue<byte>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(string)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => { var pos = recordsOffset + data.Offset + (data.Position >> 3); int strOfs = FieldReader.GetFieldValue<int>(id, data, fieldMeta, columnMeta, palletData, commonData); return stringTable[pos + strOfs]; },
        };

        static Dictionary<Type, Func<BitReader, FieldMetaData, ColumnMetaData, Value32[], Dictionary<int, Value32>, Dictionary<long, string>, int, object>> arrayReaders = new Dictionary<Type, Func<BitReader, FieldMetaData, ColumnMetaData, Value32[], Dictionary<int, Value32>, Dictionary<long, string>, int, object>>
        {
            [typeof(float)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => FieldReader.GetFieldValueArray<float>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex],
            [typeof(int)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => FieldReader.GetFieldValueArray<int>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex],
            [typeof(uint)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => FieldReader.GetFieldValueArray<uint>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex],
            [typeof(ulong)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => FieldReader.GetFieldValueArray<ulong>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex],
            [typeof(ushort)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => FieldReader.GetFieldValueArray<ushort>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex],
            [typeof(byte)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => FieldReader.GetFieldValueArray<byte>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex],
            [typeof(string)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => { int strOfs = FieldReader.GetFieldValueArray<int>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex]; return stringTable[strOfs]; },
        };

        public T GetField<T>(int fieldIndex, int arrayIndex = -1)
        {
            object value = null;

            if (fieldIndex >= m_reader.Meta.Length)
            {
                if (m_refId != -1)
                    value = m_refId;
                else
                    value = 0;
                return (T)value;
            }

            m_data.Position = m_columnMeta[fieldIndex].RecordOffset;
            m_data.Offset = m_dataOffset;

            if (arrayIndex >= 0)
            {
                if (arrayReaders.TryGetValue(typeof(T), out var reader))
                    value = reader(m_data, m_fieldMeta[fieldIndex], m_columnMeta[fieldIndex], m_palletData[fieldIndex], m_commonData[fieldIndex], m_stringsTable, arrayIndex);
                else
                    throw new Exception("Unhandled array type: " + typeof(T).Name);
            }
            else
            {
                if (simpleReaders.TryGetValue(typeof(T), out var reader))
                    value = reader(m_id, m_data, m_recordsOffset, m_fieldMeta[fieldIndex], m_columnMeta[fieldIndex], m_palletData[fieldIndex], m_commonData[fieldIndex], m_stringsTable);
                else
                    throw new Exception("Unhandled field type: " + typeof(T).Name);
            }

            return (T)value;
        }

        public IDB2Row Clone() => (IDB2Row)MemberwiseClone();
    }

    public class WDC2Reader : DB2Reader<WDC2Row>
    {
        const int HeaderSize = 72 + 1 * 36;
        const uint WDC2FmtSig = 0x32434457; // WDC2

        public WDC2Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public WDC2Reader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                    throw new InvalidDataException(String.Format("WDC2 file is corrupted!"));

                uint magic = reader.ReadUInt32();

                if (magic != WDC2FmtSig)
                    throw new InvalidDataException(String.Format("WDC2 file is corrupted!"));

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();
                TableHash = reader.ReadUInt32();
                LayoutHash = reader.ReadUInt32();
                MinIndex = reader.ReadInt32();
                MaxIndex = reader.ReadInt32();
                int locale = reader.ReadInt32();
                int flags = reader.ReadUInt16();
                IdFieldIndex = reader.ReadUInt16();
                int totalFieldsCount = reader.ReadInt32();
                int packedDataOffset = reader.ReadInt32(); // Offset within the field where packed data starts
                int lookupColumnCount = reader.ReadInt32(); // count of lookup columns
                int columnMetaDataSize = reader.ReadInt32(); // 24 * NumFields bytes, describes column bit packing, {ushort recordOffset, ushort size, uint additionalDataSize, uint compressionType, uint packedDataOffset or commonvalue, uint cellSize, uint cardinality}[NumFields], sizeof(DBC2CommonValue) == 8
                int commonDataSize = reader.ReadInt32();
                int palletDataSize = reader.ReadInt32(); // in bytes, sizeof(DBC2PalletValue) == 4
                int sectionsCount = reader.ReadInt32();

                if (sectionsCount > 1)
                    throw new Exception("sectionsCount > 1");

                SectionHeader_WDC2[] sections = reader.ReadArray<SectionHeader_WDC2>(sectionsCount);

                // field meta data
                m_meta = reader.ReadArray<FieldMetaData>(FieldsCount);

                // column meta data
                m_columnMeta = reader.ReadArray<ColumnMetaData>(FieldsCount);

                // pallet data
                m_palletData = new Value32[m_columnMeta.Length][];

                for (int i = 0; i < m_columnMeta.Length; i++)
                {
                    if (m_columnMeta[i].CompressionType == CompressionType.Pallet || m_columnMeta[i].CompressionType == CompressionType.PalletArray)
                    {
                        m_palletData[i] = reader.ReadArray<Value32>((int)m_columnMeta[i].AdditionalDataSize / 4);
                    }
                }

                // common data
                m_commonData = new Dictionary<int, Value32>[m_columnMeta.Length];

                for (int i = 0; i < m_columnMeta.Length; i++)
                {
                    if (m_columnMeta[i].CompressionType == CompressionType.Common)
                    {
                        Dictionary<int, Value32> commonValues = new Dictionary<int, Value32>();
                        m_commonData[i] = commonValues;

                        for (int j = 0; j < m_columnMeta[i].AdditionalDataSize / 8; j++)
                            commonValues[reader.ReadInt32()] = reader.Read<Value32>();
                    }
                }

                for (int sectionIndex = 0; sectionIndex < sectionsCount; sectionIndex++)
                {
                    reader.BaseStream.Position = sections[sectionIndex].FileOffset;

                    byte[] recordsData;
                    Dictionary<long, string> stringsTable = null;
                    SparseEntry[] sparseEntries = null;

                    if ((flags & 0x1) == 0)
                    {
                        // records data
                        recordsData = reader.ReadBytes(sections[sectionIndex].NumRecords * RecordSize);

                        Array.Resize(ref recordsData, recordsData.Length + 8); // pad with extra zeros so we don't crash when reading

                        // string data
                        stringsTable = new Dictionary<long, string>();

                        for (int i = 0; i < sections[sectionIndex].StringTableSize;)
                        {
                            long oldPos = reader.BaseStream.Position;

                            stringsTable[oldPos] = Ext.ReadCString(reader);

                            i += (int)(reader.BaseStream.Position - oldPos);
                        }
                    }
                    else
                    {
                        // sparse data with inlined strings
                        recordsData = reader.ReadBytes(sections[sectionIndex].SparseTableOffset - sections[sectionIndex].FileOffset);

                        if (reader.BaseStream.Position != sections[sectionIndex].SparseTableOffset)
                            throw new Exception("reader.BaseStream.Position != sections[sectionIndex].SparseTableOffset");

                        sparseEntries = reader.ReadArray<SparseEntry>(MaxIndex - MinIndex + 1);

                        if (sections[sectionIndex].SparseTableOffset != 0)
                            throw new Exception("Sparse Table NYI!");
                        else
                            throw new Exception("Sparse Table with zero offset?");
                    }

                    // index data
                    int[] indexData = reader.ReadArray<int>(sections[sectionIndex].IndexDataSize / 4);

                    // duplicate rows data
                    Dictionary<int, int> copyData = new Dictionary<int, int>();

                    for (int i = 0; i < sections[sectionIndex].CopyTableSize / 8; i++)
                        copyData[reader.ReadInt32()] = reader.ReadInt32();

                    // reference data
                    ReferenceData refData = null;

                    if (sections[sectionIndex].ParentLookupDataSize > 0)
                    {
                        refData = new ReferenceData
                        {
                            NumRecords = reader.ReadInt32(),
                            MinId = reader.ReadInt32(),
                            MaxId = reader.ReadInt32()
                        };

                        ReferenceEntry[] entries = reader.ReadArray<ReferenceEntry>(refData.NumRecords);
                        refData.Entries = entries.ToDictionary(e => e.Index, e => e.Id);
                    }
                    else
                    {
                        refData = new ReferenceData
                        {
                            Entries = new Dictionary<int, int>()
                        };
                    }

                    BitReader bitReader = new BitReader(recordsData);

                    for (int i = 0; i < RecordsCount; ++i)
                    {
                        bitReader.Position = 0;
                        bitReader.Offset = i * RecordSize;

                        bool hasRef = refData.Entries.TryGetValue(i, out int refId);

                        WDC2Row rec = new WDC2Row(this, bitReader, sections[sectionIndex].FileOffset, sections[sectionIndex].IndexDataSize != 0 ? indexData[i] : -1, hasRef ? refId : -1, stringsTable);

                        if (sections[sectionIndex].IndexDataSize != 0)
                            _Records.Add(indexData[i], rec);
                        else
                            _Records.Add(rec.GetId(), rec);

                        if (i % 1000 == 0)
                            Console.Write("\r{0} records read", i);
                    }

                    foreach (var copyRow in copyData)
                    {
                        WDC2Row rec = (WDC2Row)_Records[copyRow.Value].Clone();
                        rec.SetId(copyRow.Key);
                        _Records.Add(copyRow.Key, rec);
                    }
                }
            }
        }
    }
}
