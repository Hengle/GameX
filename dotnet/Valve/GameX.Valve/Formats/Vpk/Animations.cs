using OpenStack.Gfx;
using OpenStack.Gfx.Animates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SystemX;

namespace GameX.Valve.Formats.Vpk
{
    #region Animation
    //was:Resource/ResourceTypes/ModelAnimation/Animation

    public class Animation : IAnimation
    {
        public string Name { get; private set; }
        public float Fps { get; private set; }
        public int FrameCount { get; private set; }
        AnimationFrameBlock[] FrameBlocks { get; }
        AnimationSegmentDecoder[] SegmentArray { get; }

        Animation(IDictionary<string, object> animDesc, AnimationSegmentDecoder[] segmentArray)
        {
            // Get animation properties
            Name = animDesc.Get<string>("m_name");
            Fps = animDesc.GetFloat("fps");
            SegmentArray = segmentArray;

            var pDataObject = animDesc.Get<object>("m_pData");
            var pData = pDataObject is object[] ntroArray
                ? ntroArray[0] as IDictionary<string, object>
                : pDataObject as IDictionary<string, object>;
            FrameCount = pData.GetInt32("m_nFrames");

            var frameBlockArray = pData.GetArray("m_frameblockArray");
            FrameBlocks = new AnimationFrameBlock[frameBlockArray.Length];
            for (var i = 0; i < frameBlockArray.Length; i++) FrameBlocks[i] = new AnimationFrameBlock(frameBlockArray[i]);
        }

        public static IEnumerable<Animation> FromData(IDictionary<string, object> animationData, IDictionary<string, object> decodeKey, Skeleton skeleton)
        {
            var animArray = animationData.Get<IDictionary<string, object>[]>("m_animArray");
            if (animArray.Length == 0)
            {
                Console.WriteLine("Empty animation file found.");
                return [];
            }

            var decoderArrayKV = animationData.GetArray("m_decoderArray");
            var decoderArray = new string[decoderArrayKV.Length];
            for (var i = 0; i < decoderArrayKV.Length; i++) decoderArray[i] = decoderArrayKV[i].Get<string>("m_szName");

            var channelElements = decodeKey.GetInt32("m_nChannelElements");
            var dataChannelArrayKV = decodeKey.GetArray("m_dataChannelArray");
            var dataChannelArray = new AnimationDataChannel[dataChannelArrayKV.Length];
            for (var i = 0; i < dataChannelArrayKV.Length; i++) dataChannelArray[i] = new AnimationDataChannel(skeleton, dataChannelArrayKV[i], channelElements);

            var segmentArrayKV = animationData.GetArray("m_segmentArray");
            var segmentArray = new AnimationSegmentDecoder[segmentArrayKV.Length];
            for (var i = 0; i < segmentArrayKV.Length; i++)
            {
                var segmentKV = segmentArrayKV[i];
                var container = segmentKV.Get<byte[]>("m_container");
                var localChannel = dataChannelArray[segmentKV.GetInt32("m_nLocalChannel")];
                using var containerReader = new BinaryReader(new MemoryStream(container));

                // Read header
                var decoder = decoderArray[containerReader.ReadInt16()];
                var cardinality = containerReader.ReadInt16();
                var numElements = containerReader.ReadInt16();
                var totalLength = containerReader.ReadInt16();

                // Read bone list
                var elements = new int[numElements];
                for (var j = 0; j < numElements; j++) elements[j] = containerReader.ReadInt16();

                var containerSegment = new ArraySegment<byte>(container, (int)containerReader.BaseStream.Position, container.Length - (int)containerReader.BaseStream.Position);
                var remapTable = localChannel.RemapTable.Select(i => Array.IndexOf(elements, i)).ToArray();
                var wantedElements = remapTable.Where(boneID => boneID != -1).ToArray();
                remapTable = remapTable.Select((boneID, i) => (boneID, i)).Where(t => t.boneID != -1).Select(t => t.i).ToArray();
                var channelAttribute = localChannel.ChannelAttribute switch
                {
                    "Position" => ChannelAttribute.Position,
                    "Angle" => ChannelAttribute.Angle,
                    "Scale" => ChannelAttribute.Scale,
                    _ => ChannelAttribute.Unknown,
                };

                if (channelAttribute == ChannelAttribute.Unknown)
                {
                    if (localChannel.ChannelAttribute != "data") Console.Error.WriteLine($"Unknown channel attribute '{localChannel.ChannelAttribute}' encountered with '{decoder}' decoder");
                    continue;
                }

                // Look at the decoder to see what to read
                switch (decoder)
                {
                    case "CCompressedStaticFullVector3": segmentArray[i] = new CCompressedStaticFullVector3(containerSegment, wantedElements, remapTable, channelAttribute); break;
                    case "CCompressedStaticVector3": segmentArray[i] = new CCompressedStaticVector3(containerSegment, wantedElements, remapTable, channelAttribute); break;
                    case "CCompressedStaticQuaternion": segmentArray[i] = new CCompressedStaticQuaternion(containerSegment, wantedElements, remapTable, channelAttribute); break;
                    case "CCompressedStaticFloat": segmentArray[i] = new CCompressedStaticFloat(containerSegment, wantedElements, remapTable, channelAttribute); break;
                    case "CCompressedFullVector3": segmentArray[i] = new CCompressedFullVector3(containerSegment, wantedElements, remapTable, numElements, channelAttribute); break;
                    case "CCompressedDeltaVector3": segmentArray[i] = new CCompressedDeltaVector3(containerSegment, wantedElements, remapTable, numElements, channelAttribute); break;
                    case "CCompressedAnimVector3": segmentArray[i] = new CCompressedAnimVector3(containerSegment, wantedElements, remapTable, numElements, channelAttribute); break;
                    case "CCompressedAnimQuaternion": segmentArray[i] = new CCompressedAnimQuaternion(containerSegment, wantedElements, remapTable, numElements, channelAttribute); break;
                    case "CCompressedFullQuaternion": segmentArray[i] = new CCompressedFullQuaternion(containerSegment, wantedElements, remapTable, numElements, channelAttribute); break;
                    case "CCompressedFullFloat": segmentArray[i] = new CCompressedFullFloat(containerSegment, wantedElements, remapTable, numElements, channelAttribute); break;
#if DEBUG
                    default: if (localChannel.ChannelAttribute != "data") Console.WriteLine($"Unhandled animation bone decoder type '{decoder}' for attribute '{localChannel.ChannelAttribute}'"); break;
#endif
                }
            }

            return animArray.Select(anim => new Animation(anim, segmentArray)).ToArray();
        }

        public static IEnumerable<Animation> FromResource(Binary_Src parent, IDictionary<string, object> decodeKey, Skeleton skeleton) => FromData(GetAnimationData(parent), decodeKey, skeleton);

        static IDictionary<string, object> GetAnimationData(Binary_Src parent) => parent.DATA.AsKeyValue();

        /// <summary>
        /// Get the animation matrix for each bone.
        /// </summary>
        //public Matrix4x4[] GetAnimationMatrices(FrameCache frameCache, int frameIndex, ISkeleton skeleton)
        //{
        //    // Get bone transformations
        //    var frame = frameCache.GetFrame(this, frameIndex);
        //    return GetAnimationMatrices(frame, skeleton);
        //}

        /// <summary>
        /// Get the animation matrix for each bone.
        /// </summary>
        public Matrix4x4[] GetAnimationMatrices(FrameCache frameCache, object index, ISkeleton skeleton)
        {
            // Get bone transformations
            var frame = FrameCount != 0 ? frameCache.GetFrame(this, index) : null;
            return GetAnimationMatrices(frame, skeleton);
        }

        Matrix4x4[] GetAnimationMatrices(Frame frame, ISkeleton skeleton)
        {
            // Create output array
            var matrices = new Matrix4x4[skeleton.Bones.Length];
            foreach (var root in skeleton.Roots) GetAnimationMatrixRecursive(root, Matrix4x4.Identity, Matrix4x4.Identity, frame, ref matrices);
            return matrices;
        }

        public void DecodeFrame(int frameIndex, Frame outFrame)
        {
            // Read all frame blocks
            foreach (var frameBlock in FrameBlocks)
                // Only consider blocks that actual contain info for this frame
                if (frameIndex >= frameBlock.StartFrame && frameIndex <= frameBlock.EndFrame)
                    foreach (var segmentIndex in frameBlock.SegmentIndexArray)
                    {
                        var segment = SegmentArray[segmentIndex];
                        // Segment could be null for unknown decoders
                        if (segment != null) segment.Read(frameIndex - frameBlock.StartFrame, outFrame);
                    }
        }

        /// <summary>
        /// Get animation matrix recursively.
        /// </summary>
        void GetAnimationMatrixRecursive(Bone bone, Matrix4x4 bindPose, Matrix4x4 invBindPose, Frame frame, ref Matrix4x4[] matrices)
        {
            // Calculate world space inverse bind pose
            invBindPose *= bone.InverseBindPose;

            // Calculate and apply tranformation matrix
            if (frame != null)
            {
                var transform = frame.Bones[bone.Index];
                bindPose = Matrix4x4.CreateScale(transform.Scale)
                    * Matrix4x4.CreateFromQuaternion(transform.Angle)
                    * Matrix4x4.CreateTranslation(transform.Position)
                    * bindPose;
            }
            else bindPose = bone.BindPose * bindPose;

            // Store result
            var skinMatrix = invBindPose * bindPose;
            matrices[bone.Index] = skinMatrix;

            // Propagate to childen
            foreach (var child in bone.Children) GetAnimationMatrixRecursive(child, bindPose, invBindPose, frame, ref matrices);
        }

        public override string ToString() => Name;
    }

    #endregion

    #region AnimationDataChannel
    //was:Resource/ResourceTypes/ModelAnimation/AnimationDataChannel

    public class AnimationDataChannel
    {
        public int[] RemapTable { get; } // Bone ID => Element Index
        public string ChannelAttribute { get; }

        public AnimationDataChannel(Skeleton skeleton, IDictionary<string, object> dataChannel, int channelElements)
        {
            RemapTable = Enumerable.Range(0, skeleton.Bones.Length).Select(_ => -1).ToArray();
            var elementNameArray = dataChannel.Get<string[]>("m_szElementNameArray");
            var elementIndexArray = dataChannel.Get<int[]>("m_nElementIndexArray");
            for (var i = 0; i < elementIndexArray.Length; i++)
            {
                var elementName = elementNameArray[i];
                var boneID = Array.FindIndex(skeleton.Bones, bone => bone.Name == elementName);
                if (boneID != -1) RemapTable[boneID] = (int)elementIndexArray[i];
            }
            ChannelAttribute = dataChannel.Get<string>("m_szVariableName");
        }
    }

    #endregion

    #region AnimationFrameBlock
    //was:Resource/ResourceTypes/ModelAnimation/AnimationFrameBlock

    public class AnimationFrameBlock(IDictionary<string, object> frameBlock)
    {
        public int StartFrame { get; } = frameBlock.GetInt32("m_nStartFrame");
        public int EndFrame { get; } = frameBlock.GetInt32("m_nEndFrame");
        public long[] SegmentIndexArray { get; } = frameBlock.GetInt64Array("m_segmentIndexArray");
    }

    #endregion

    #region AnimationGroupLoader
    //was:Resource/ResourceTypes/ModelAnimation/AnimationGroupLoader

    public static class AnimationGroupLoader
    {
        public static IEnumerable<Animation> LoadAnimationGroup(Binary_Src resource, IOpenGfx gfx, Skeleton skeleton)
        {
            var data = resource.DATA.AsKeyValue();
            var decodeKey = data.GetSub("m_decodeKey"); // Get the key to decode the animations

            // Load animation files
            var animationList = new List<Animation>();
            if (resource.ContainsBlockType<ANIM>())
            {
                var animBlock = (XKV3_NTRO)resource.GetBlockByType<ANIM>();
                animationList.AddRange(Animation.FromData(animBlock.Data, decodeKey, skeleton));
                return animationList;
            }
            var animArray = data.Get<string[]>("m_localHAnimArray").Where(a => a != null); // Get the list of animation files
            foreach (var animationFile in animArray)
            {
                var animResource = gfx.LoadFileObject<Binary_Src>($"{animationFile}_c").Result;
                if (animResource != null) animationList.AddRange(Animation.FromResource(animResource, decodeKey, skeleton)); // Build animation classes
            }
            return animationList;
        }
    }

    #endregion

    #region AnimationSegmentDecoder

    //was:Resource/ResourceTypes/ModelAnimation/AnimationSegmentDecoder
    public abstract class AnimationSegmentDecoder(int[] remapTable, ChannelAttribute channelAttribute)
    {
        public int[] RemapTable { get; } = remapTable;
        public ChannelAttribute ChannelAttribute { get; } = channelAttribute;

        public abstract void Read(int frameIndex, Frame outFrame);

        //was:Resource/ResourceTypes/ModelAnimation/SegmentHelpers
        /// <summary>
        /// Read and decode encoded quaternion.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <returns>Quaternion.</returns>
        internal static Quaternion ReadQuaternion(ReadOnlySpan<byte> bytes)
        {
            // Values
            var i1 = bytes[0] + ((bytes[1] & 63) << 8);
            var i2 = bytes[2] + ((bytes[3] & 63) << 8);
            var i3 = bytes[4] + ((bytes[5] & 63) << 8);

            // Signs
            var s1 = bytes[1] & 128;
            var s2 = bytes[3] & 128;
            var s3 = bytes[5] & 128;

            var c = MathF.Sin(MathF.PI / 4.0f) / 16384.0f;
            var x = (bytes[1] & 64) == 0 ? c * (i1 - 16384) : c * i1;
            var y = (bytes[3] & 64) == 0 ? c * (i2 - 16384) : c * i2;
            var z = (bytes[5] & 64) == 0 ? c * (i3 - 16384) : c * i3;

            var w = MathF.Sqrt(1 - x * x - y * y - z * z);

            // Apply sign 3
            if (s3 == 128) w *= -1;

            // Apply sign 1 and 2
            return s1 == 128
                ? s2 == 128 ? new Quaternion(y, z, w, x) : new Quaternion(z, w, x, y)
                : s2 == 128 ? new Quaternion(w, x, y, z) : new Quaternion(x, y, z, w);
        }
    }

    #endregion

    #region AnimationSegmentDecoder : CCompressedAnimQuaternion
    //was:Resource/ResourceTypes/ModelAnimation/SegmentDecoders/CCompressedAnimQuaternion

    public class CCompressedAnimQuaternion : AnimationSegmentDecoder
    {
        readonly byte[] Data;

        public CCompressedAnimQuaternion(ArraySegment<byte> data, int[] wantedElements, int[] remapTable, int elementCount, ChannelAttribute channelAttribute) : base(remapTable, channelAttribute)
        {
            const int elementSize = 6;
            var stride = elementCount * elementSize;
            var elements = data.Count / stride;

            Data = new byte[remapTable.Length * elementSize * elements];
            var pos = 0;
            for (var i = 0; i < elements; i++)
                foreach (var j in wantedElements)
                {
                    data.Slice(i * stride + j * elementSize, elementSize).CopyTo(Data, pos);
                    pos += elementSize;
                }
        }

        public override void Read(int frameIndex, Frame outFrame)
        {
            const int elementSize = 6;
            var offset = frameIndex * RemapTable.Length * elementSize;
            for (var i = 0; i < RemapTable.Length; i++)
                outFrame.SetAttribute(
                    RemapTable[i],
                    ChannelAttribute,
                    ReadQuaternion(new ReadOnlySpan<byte>(
                        Data,
                        offset + i * elementSize,
                        elementSize
                    ))
                );
        }
    }

    #endregion

    #region AnimationSegmentDecoder : CCompressedAnimVector3
    //was:Resource/ResourceTypes/ModelAnimation/SegmentDecoders/CCompressedAnimVector3

    public class CCompressedAnimVector3 : AnimationSegmentDecoder
    {
        readonly Half[] Data;

        public CCompressedAnimVector3(ArraySegment<byte> data, int[] wantedElements, int[] remapTable, int elementCount, ChannelAttribute channelAttribute) : base(remapTable, channelAttribute)
        {
            const int elementSize = 2;
            var stride = elementCount * elementSize;
            var elements = data.Count / stride;

            Data = new Half[remapTable.Length * elements];
            var pos = 0;
            for (var i = 0; i < elements; i++)
                foreach (var j in wantedElements)
                    Data[pos++] = BitConverterX.ToHalf(data.Slice(i * stride + j * elementSize, elementSize));
        }

        public override void Read(int frameIndex, Frame outFrame)
        {
            var offset = frameIndex * RemapTable.Length * 3;
            for (var i = 0; i < RemapTable.Length; i++)
                outFrame.SetAttribute(
                    RemapTable[i],
                    ChannelAttribute,
                    new Vector3(
                        (float)Data[offset + i * 3 + 0],
                        (float)Data[offset + i * 3 + 1],
                        (float)Data[offset + i * 3 + 2]
                    )
                );
        }
    }

    #endregion

    #region AnimationSegmentDecoder : CCompressedDeltaVector3
    //was:Resource/ResourceTypes/ModelAnimation/SegmentDecoders/CCompressedDeltaVector3

    public class CCompressedDeltaVector3 : AnimationSegmentDecoder
    {
        readonly Vector3[] BaseFrame;
        readonly Half[] DeltaData;

        public CCompressedDeltaVector3(ArraySegment<byte> data, int[] wantedElements, int[] remapTable, int elementCount, ChannelAttribute channelAttribute) : base(remapTable, channelAttribute)
        {
            const int baseElementSize = 4;
            const int deltaElementSize = 2;

            BaseFrame = new Vector3[wantedElements.Length];

            var pos = 0;
            foreach (var i in wantedElements)
            {
                var offset = i * 3 * baseElementSize;
                BaseFrame[pos++] = new Vector3(
                    BitConverter.ToSingle(data.Slice(offset + (0 * baseElementSize), baseElementSize)),
                    BitConverter.ToSingle(data.Slice(offset + (1 * baseElementSize), baseElementSize)),
                    BitConverter.ToSingle(data.Slice(offset + (2 * baseElementSize), baseElementSize))
                );
            }

            var deltaData = data.Slice(elementCount * 3 * baseElementSize);
            var stride = elementCount * deltaElementSize;
            var elements = deltaData.Count / stride;

            DeltaData = new Half[remapTable.Length * elements];

            pos = 0;
            for (var i = 0; i < elements; i++)
                foreach (var j in wantedElements)
                    DeltaData[pos++] = BitConverterX.ToHalf(deltaData.Slice(i * stride + j * deltaElementSize, deltaElementSize));
        }

        public override void Read(int frameIndex, Frame outFrame)
        {
            var offset = frameIndex * RemapTable.Length * 3;
            for (var i = 0; i < RemapTable.Length; i++)
            {
                var baseFrame = BaseFrame[i];
                outFrame.SetAttribute(
                    RemapTable[i],
                    ChannelAttribute,
                    baseFrame + new Vector3(
                        (float)DeltaData[offset + i * 3 + 0],
                        (float)DeltaData[offset + i * 3 + 1],
                        (float)DeltaData[offset + i * 3 + 2]
                    )
                );
            }
        }
    }

    #endregion

    #region AnimationSegmentDecoder : CCompressedFullFloat
    //was:Resource/ResourceTypes/ModelAnimation/SegmentDecoders/CCompressedFullFloat

    public class CCompressedFullFloat : AnimationSegmentDecoder
    {
        readonly float[] Data;

        public CCompressedFullFloat(ArraySegment<byte> data, int[] wantedElements, int[] remapTable, int elementCount, ChannelAttribute channelAttribute) : base(remapTable, channelAttribute)
        {
            const int elementSize = 4;
            var stride = elementCount * elementSize;
            Data = Enumerable.Range(0, data.Count / stride)
                .SelectMany(i => wantedElements.Select(j => BitConverter.ToSingle(data.Slice(i * stride + j * elementSize))).ToArray())
                .ToArray();
        }

        public override void Read(int frameIndex, Frame outFrame)
        {
            var offset = frameIndex * RemapTable.Length;
            for (var i = 0; i < RemapTable.Length; i++) outFrame.SetAttribute(RemapTable[i], ChannelAttribute, Data[offset + i]);
        }
    }

    #endregion

    #region AnimationSegmentDecoder : CCompressedFullQuaternion
    //was:Resource/ResourceTypes/ModelAnimation/SegmentDecoders/CCompressedFullQuaternion

    public class CCompressedFullQuaternion : AnimationSegmentDecoder
    {
        readonly Quaternion[] Data;

        public CCompressedFullQuaternion(ArraySegment<byte> data, int[] wantedElements, int[] remapTable, int elementCount, ChannelAttribute channelAttribute) : base(remapTable, channelAttribute)
        {
            const int elementSize = 4 * 4;
            var stride = elementCount * elementSize;
            Data = Enumerable.Range(0, data.Count / stride)
                .SelectMany(i => wantedElements.Select(j =>
                {
                    var offset = i * stride + j * elementSize;
                    return new Quaternion(
                        BitConverter.ToSingle(data.Slice(offset + (0 * 4))),
                        BitConverter.ToSingle(data.Slice(offset + (1 * 4))),
                        BitConverter.ToSingle(data.Slice(offset + (2 * 4))),
                        BitConverter.ToSingle(data.Slice(offset + (3 * 4)))
                    );
                }).ToArray())
                .ToArray();
        }

        public override void Read(int frameIndex, Frame outFrame)
        {
            var offset = frameIndex * RemapTable.Length;
            for (var i = 0; i < RemapTable.Length; i++) outFrame.SetAttribute(RemapTable[i], ChannelAttribute, Data[offset + i]);
        }
    }

    #endregion

    #region AnimationSegmentDecoder : CCompressedFullVector3
    //was:Resource/ResourceTypes/ModelAnimation/SegmentDecoders/CCompressedFullVector3

    public class CCompressedFullVector3 : AnimationSegmentDecoder
    {
        readonly Vector3[] Data;

        public CCompressedFullVector3(ArraySegment<byte> data, int[] wantedElements, int[] remapTable, int elementCount, ChannelAttribute channelAttribute) : base(remapTable, channelAttribute)
        {
            const int elementSize = 3 * 4;
            var stride = elementCount * elementSize;
            var elements = data.Count / stride;
            Data = new Vector3[remapTable.Length * elements];
            var pos = 0;
            for (var i = 0; i < elements; i++)
                foreach (var j in wantedElements)
                {
                    var offset = i * stride + j * elementSize;
                    Data[pos++] = new Vector3(
                        BitConverter.ToSingle(data.Slice(offset + (0 * 4), 4)),
                        BitConverter.ToSingle(data.Slice(offset + (1 * 4), 4)),
                        BitConverter.ToSingle(data.Slice(offset + (2 * 4), 4))
                    );
                }
        }

        public override void Read(int frameIndex, Frame outFrame)
        {
            var offset = frameIndex * RemapTable.Length;
            for (var i = 0; i < RemapTable.Length; i++) outFrame.SetAttribute(RemapTable[i], ChannelAttribute, Data[offset + i]);
        }
    }

    #endregion

    #region AnimationSegmentDecoder : CCompressedStaticFloat
    //was:Resource/ResourceTypes/ModelAnimation/SegmentDecoders/CCompressedStaticFloat

    public class CCompressedStaticFloat(ArraySegment<byte> data, int[] wantedElements, int[] remapTable, ChannelAttribute channelAttribute) : AnimationSegmentDecoder(remapTable, channelAttribute)
    {
        readonly float[] Data = wantedElements.Select(i => BitConverter.ToSingle(data.Slice(i * 4))).ToArray();

        public override void Read(int frameIndex, Frame outFrame)
        {
            for (var i = 0; i < RemapTable.Length; i++) outFrame.SetAttribute(RemapTable[i], ChannelAttribute, Data[i]);
        }
    }

    #endregion

    #region AnimationSegmentDecoder : CCompressedStaticFullVector3
    //was:Resource/ResourceTypes/ModelAnimation/SegmentDecoders/CCompressedStaticFullVector3

    public class CCompressedStaticFullVector3(ArraySegment<byte> data, int[] wantedElements, int[] remapTable, ChannelAttribute channelAttribute) : AnimationSegmentDecoder(remapTable, channelAttribute)
    {
        readonly Vector3[] Data = wantedElements.Select(i =>
        {
            var offset = i * (3 * 4);
            return new Vector3(
                BitConverter.ToSingle(data.Slice(offset + (0 * 4))),
                BitConverter.ToSingle(data.Slice(offset + (1 * 4))),
                BitConverter.ToSingle(data.Slice(offset + (2 * 4)))
            );
        }).ToArray();

        public override void Read(int frameIndex, Frame outFrame)
        {
            for (var i = 0; i < RemapTable.Length; i++) outFrame.SetAttribute(RemapTable[i], ChannelAttribute, Data[i]);
        }
    }

    #endregion

    #region AnimationSegmentDecoder : CCompressedStaticQuaternion
    //was:Resource/ResourceTypes/ModelAnimation/SegmentDecoders/CCompressedStaticQuaternion

    public class CCompressedStaticQuaternion(ArraySegment<byte> data, int[] wantedElements, int[] remapTable, ChannelAttribute channelAttribute) : AnimationSegmentDecoder(remapTable, channelAttribute)
    {
        readonly Quaternion[] Data = wantedElements.Select(i => ReadQuaternion(data.Slice(i * 6))).ToArray();

        public override void Read(int frameIndex, Frame outFrame)
        {
            for (var i = 0; i < RemapTable.Length; i++) outFrame.SetAttribute(RemapTable[i], ChannelAttribute, Data[i]);
        }
    }

    #endregion

    #region AnimationSegmentDecoder : CCompressedStaticVector3
    //was:Resource/ResourceTypes/ModelAnimation/SegmentDecoders/CCompressedStaticVector3

    public class CCompressedStaticVector3(ArraySegment<byte> data, int[] wantedElements, int[] remapTable, ChannelAttribute channelAttribute) : AnimationSegmentDecoder(remapTable, channelAttribute)
    {
        readonly Vector3[] Data = wantedElements.Select(i =>
        {
            var offset = i * (3 * 2);
            return new Vector3(
                (float)BitConverterX.ToHalf(data.Slice(offset + (0 * 2))),
                (float)BitConverterX.ToHalf(data.Slice(offset + (1 * 2))),
                (float)BitConverterX.ToHalf(data.Slice(offset + (2 * 2)))
            );
        }).ToArray();

        public override void Read(int frameIndex, Frame outFrame)
        {
            for (var i = 0; i < RemapTable.Length; i++) outFrame.SetAttribute(RemapTable[i], ChannelAttribute, Data[i]);
        }
    }

    #endregion

    #region Skeleton

    //was:Resource/ResourceTypes/ModelAnimation/Skeleton
    public class Skeleton : ISkeleton
    {
        [Flags]
        public enum ModelSkeletonBoneFlags //was:Resource/Enums/ModelSkeletonBoneFlags
        {
            NoBoneFlags = 0x0,
            BoneFlexDriver = 0x4,
            Cloth = 0x8,
            Physics = 0x10,
            Attachment = 0x20,
            Animation = 0x40,
            Mesh = 0x80,
            Hitbox = 0x100,
            RetargetSrc = 0x200,
            BoneUsedByVertexLod0 = 0x400,
            BoneUsedByVertexLod1 = 0x800,
            BoneUsedByVertexLod2 = 0x1000,
            BoneUsedByVertexLod3 = 0x2000,
            BoneUsedByVertexLod4 = 0x4000,
            BoneUsedByVertexLod5 = 0x8000,
            BoneUsedByVertexLod6 = 0x10000,
            BoneUsedByVertexLod7 = 0x20000,
            BoneMergeRead = 0x40000,
            BoneMergeWrite = 0x80000,
            BlendPrealigned = 0x100000,
            RigidLength = 0x200000,
            Procedural = 0x400000,
        }

        public Bone[] Roots { get; private set; }
        public Bone[] Bones { get; private set; }
        public int[] LocalRemapTable { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Skeleton"/> class.
        /// </summary>
        public static Skeleton FromModelData(IDictionary<string, object> modelData)
        {
            // Check if there is any skeleton data present at all
            if (!modelData.ContainsKey("m_modelSkeleton")) Console.WriteLine("No skeleton data found.");
            // Construct the armature from the skeleton KV
            return new Skeleton(modelData.GetSub("m_modelSkeleton"));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Skeleton"/> class.
        /// </summary>
        public Skeleton(IDictionary<string, object> skeletonData)
        {
            var boneNames = skeletonData.Get<string[]>("m_boneName");
            var boneParents = skeletonData.GetInt64Array("m_nParent");
            var boneFlags = skeletonData.GetInt64Array("m_nFlag").Select(flags => (ModelSkeletonBoneFlags)flags).ToArray();
            var bonePositions = skeletonData.Get<Vector3[]>("m_bonePosParent");
            var boneRotations = skeletonData.Get<Quaternion[]>("m_boneRotParent");

            LocalRemapTable = new int[boneNames.Length];
            var currentRemappedBone = 0;
            for (var i = 0; i < LocalRemapTable.Length; i++)
                LocalRemapTable[i] = (boneFlags[i] & ModelSkeletonBoneFlags.BoneUsedByVertexLod0) != 0
                    ? currentRemappedBone++
                    : -1;

            // Initialise bone array
            Bones = Enumerable.Range(0, boneNames.Length)
                .Where(i => (boneFlags[i] & ModelSkeletonBoneFlags.BoneUsedByVertexLod0) != 0)
                .Select((boneID, i) => new Bone(i, boneNames[boneID], bonePositions[boneID], boneRotations[boneID]))
                .ToArray();

            for (var i = 0; i < LocalRemapTable.Length; i++)
            {
                var remappeBoneID = LocalRemapTable[i];
                if (remappeBoneID != -1 && boneParents[i] != -1)
                {
                    var remappedParent = LocalRemapTable[boneParents[i]];
                    Bones[remappeBoneID].SetParent(Bones[remappedParent]);
                }
            }

            // Create an empty root list
            Roots = Bones.Where(bone => bone.Parent == null).ToArray();
        }
    }

    #endregion
}
