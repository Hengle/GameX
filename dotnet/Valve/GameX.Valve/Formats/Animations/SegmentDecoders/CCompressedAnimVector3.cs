using OpenStack.Graphics.Renderer1.Animations;
using System;
using SystemX;
using System.Numerics;

namespace GameX.Valve.Formats.Animations.SegmentDecoders
{
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
}
