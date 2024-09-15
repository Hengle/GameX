using GameX.Meta;
using GameX.WB.Formats.AC.Entity;
using GameX.WB.Formats.AC.Props;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace GameX.WB.Formats.AC.AnimationHooks
{
    #region AttackHook
    //: Entity.AttackHook
    public class AttackHook : AnimationHook, IHaveMetaInfo
    {
        public readonly AttackCone AttackCone;

        public AttackHook(AnimationHook hook) : base(hook) { }
        public AttackHook(BinaryReader r) : base(r)
            => AttackCone = new AttackCone(r);

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is AttackHook attackHook) nodes.AddRange((attackHook.AttackCone as IHaveMetaInfo).GetInfoNodes(tag: tag));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region CallPESHook
    //: Entity.CallPESHook
    public class CallPESHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint PES;
        public readonly float Pause;

        public CallPESHook(AnimationHook hook) : base(hook) { }
        public CallPESHook(BinaryReader r) : base(r)
        {
            PES = r.ReadUInt32();
            Pause = r.ReadSingle();
        }

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is CallPESHook s) nodes.Add(new MetaInfo($"PES: {s.PES:X8}, Pause: {s.Pause}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region CreateBlockingParticle
    public class CreateBlockingParticle(BinaryReader r) : CreateParticleHook(r) { }
    #endregion

    #region CreateParticleHook
    //: Entity.CreateParticleHook
    public class CreateParticleHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint EmitterInfoId;
        public readonly uint PartIndex;
        public readonly Frame Offset;
        public readonly uint EmitterId;

        public CreateParticleHook(AnimationHook hook) : base(hook) { }
        public CreateParticleHook(BinaryReader r) : base(r)
        {
            EmitterInfoId = r.ReadUInt32();
            PartIndex = r.ReadUInt32();
            Offset = new Frame(r);
            EmitterId = r.ReadUInt32();
        }

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is CreateParticleHook s)
            {
                nodes.Add(new($"EmitterInfoId: {s.EmitterInfoId:X8}"));
                nodes.Add(new($"PartIndex: {(int)s.PartIndex}"));
                nodes.Add(new($"Offset: {s.Offset}"));
                nodes.Add(new($"EmitterId: {s.EmitterId}"));
            }
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region DefaultScriptPartHook
    //: Entity.DefaultScriptPartHook
    public class DefaultScriptPartHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint PartIndex;

        public DefaultScriptPartHook(AnimationHook hook) : base(hook) { }
        public DefaultScriptPartHook(BinaryReader r) : base(r)
            => PartIndex = r.ReadUInt32();

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is DefaultScriptPartHook s) nodes.Add(new MetaInfo($"PartIndex: {s.PartIndex}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region DestroyParticleHook
    //: Entity.DestroyParticleHook
    public class DestroyParticleHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint EmitterId;

        public DestroyParticleHook(AnimationHook hook) : base(hook) { }
        public DestroyParticleHook(BinaryReader r) : base(r)
            => EmitterId = r.ReadUInt32();

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is DestroyParticleHook s) nodes.Add(new MetaInfo($"EmitterId: {s.EmitterId}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region DiffuseHook
    //: Entity.DiffuseHook
    public class DiffuseHook : AnimationHook, IHaveMetaInfo
    {
        public readonly float Start;
        public readonly float End;
        public readonly float Time;

        public DiffuseHook(AnimationHook hook) : base(hook) { }
        public DiffuseHook(BinaryReader r) : base(r)
        {
            Start = r.ReadSingle();
            End = r.ReadSingle();
            Time = r.ReadSingle();
        }

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is DiffuseHook s) nodes.Add(new MetaInfo($"Start: {s.Start}, End: {s.End}, Time: {s.Time}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region DiffusePartHook
    //: Entity.DiffusePartHook
    public class DiffusePartHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint Part;
        public readonly float Start;
        public readonly float End;
        public readonly float Time;

        public DiffusePartHook(AnimationHook hook) : base(hook) { }
        public DiffusePartHook(BinaryReader r) : base(r)
        {
            Part = r.ReadUInt32();
            Start = r.ReadSingle();
            End = r.ReadSingle();
            Time = r.ReadSingle();
        }

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is DiffusePartHook s)
            {
                nodes.Add(new($"Part: {s.Part}"));
                nodes.Add(new($"Start: {s.Start}, End: {s.End}, Time: {s.Time}"));
            }
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region EtherealHook
    //: Entity.EtherealHook
    public class EtherealHook : AnimationHook, IHaveMetaInfo
    {
        public readonly int Ethereal;

        public EtherealHook(AnimationHook hook) : base(hook) { }
        public EtherealHook(BinaryReader r) : base(r)
            => Ethereal = r.ReadInt32();

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is EtherealHook s) nodes.Add(new MetaInfo($"Ethereal: {s.Ethereal}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region LuminousHook
    //: Entity.LuminousHook
    public class LuminousHook : AnimationHook, IHaveMetaInfo
    {
        public readonly float Start;
        public readonly float End;
        public readonly float Time;

        public LuminousHook(AnimationHook hook) : base(hook) { }
        public LuminousHook(BinaryReader r) : base(r)
        {
            Start = r.ReadSingle();
            End = r.ReadSingle();
            Time = r.ReadSingle();
        }

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is LuminousHook s) nodes.Add(new MetaInfo($"Start: {s.Start}, End: {s.End}, Time: {s.Time}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region LuminousPartHook
    //: Entity.LuminousPartHook
    public class LuminousPartHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint Part;
        public readonly float Start;
        public readonly float End;
        public readonly float Time;

        public LuminousPartHook(AnimationHook hook) : base(hook) { }
        public LuminousPartHook(BinaryReader r) : base(r)
        {
            Part = r.ReadUInt32();
            Start = r.ReadSingle();
            End = r.ReadSingle();
            Time = r.ReadSingle();
        }

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is LuminousPartHook s)
            {
                nodes.Add(new($"Part: {s.Part}"));
                nodes.Add(new($"Start: {s.Start}, End: {s.End}, Time: {s.Time}"));
            }
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region NoDrawHook
    //: Entity.NoDrawHook
    public class NoDrawHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint NoDraw;

        public NoDrawHook(AnimationHook hook) : base(hook) { }
        public NoDrawHook(BinaryReader r) : base(r)
            => NoDraw = r.ReadUInt32();

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is NoDrawHook s) nodes.Add(new MetaInfo($"NoDraw: {s.NoDraw}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region ReplaceObjectHook
    //: Entity.ReplaceObjectHook
    public class ReplaceObjectHook : AnimationHook, IHaveMetaInfo
    {
        public readonly AnimationPartChange APChange;

        public ReplaceObjectHook(AnimationHook hook) : base(hook) { }
        public ReplaceObjectHook(BinaryReader r) : base(r)
            => APChange = new AnimationPartChange(r, r.ReadUInt16());

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is ReplaceObjectHook s) nodes.AddRange((s.APChange as IHaveMetaInfo).GetInfoNodes(tag: tag));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region ScaleHook
    //: Entity.ScaleHook
    public class ScaleHook : AnimationHook, IHaveMetaInfo
    {
        public readonly float End;
        public readonly float Time;

        public ScaleHook(AnimationHook hook) : base(hook) { }
        public ScaleHook(BinaryReader r) : base(r)
        {
            End = r.ReadSingle();
            Time = r.ReadSingle();
        }

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is ScaleHook s) nodes.Add(new MetaInfo($"End: {s.End}, Time: {s.Time}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region SetLightHook
    //: Entity.SetLightHook
    public class SetLightHook : AnimationHook, IHaveMetaInfo
    {
        public readonly int LightsOn;

        public SetLightHook(AnimationHook hook) : base(hook) { }
        public SetLightHook(BinaryReader r) : base(r)
            => LightsOn = r.ReadInt32();

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is SetLightHook s) nodes.Add(new MetaInfo($"LightsOn: {s.LightsOn}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region SetOmegaHook
    //: Entity.SetOmegaHook
    public class SetOmegaHook : AnimationHook, IHaveMetaInfo
    {
        public readonly Vector3 Axis;

        public SetOmegaHook(AnimationHook hook) : base(hook) { }
        public SetOmegaHook(BinaryReader r) : base(r)
            => Axis = r.ReadVector3();

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is SetOmegaHook s) nodes.Add(new MetaInfo($"Axis: {s.Axis}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region SoundHook
    //: Entity.SoundHook
    public class SoundHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint Id;

        public SoundHook(AnimationHook hook) : base(hook) { }
        public SoundHook(BinaryReader r) : base(r)
            => Id = r.ReadUInt32();

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is SoundHook s) nodes.Add(new MetaInfo($"Id: {s.Id:X8}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region SoundTableHook
    //: Entity.SoundTableHook
    public class SoundTableHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint SoundType;

        public SoundTableHook(AnimationHook hook) : base(hook) { }
        public SoundTableHook(BinaryReader r) : base(r)
            => SoundType = r.ReadUInt32();

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is SoundTableHook s) nodes.Add(new MetaInfo($"SoundType: {(Sound)s.SoundType}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region SoundTweakedHook
    //: Entity.SoundTweakedHook
    public class SoundTweakedHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint SoundID;
        public readonly float Priority;
        public readonly float Probability;
        public readonly float Volume;

        public SoundTweakedHook(AnimationHook hook) : base(hook) { }
        public SoundTweakedHook(BinaryReader r) : base(r)
        {
            SoundID = r.ReadUInt32();
            Priority = r.ReadSingle();
            Probability = r.ReadSingle();
            Volume = r.ReadSingle();
        }

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is SoundTweakedHook s)
            {
                nodes.Add(new($"SoundID: {s.SoundID:X8}"));
                nodes.Add(new($"Priority: {s.Priority}"));
                nodes.Add(new($"Probability: {s.Probability}"));
                nodes.Add(new($"Volume: {s.Volume}"));
            }
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region StopParticleHook
    //: Entity.StopParticleHook
    public class StopParticleHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint EmitterId;

        public StopParticleHook(AnimationHook hook) : base(hook) { }
        public StopParticleHook(BinaryReader r) : base(r)
            => EmitterId = r.ReadUInt32();

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is StopParticleHook s) nodes.Add(new MetaInfo($"EmitterId: {s.EmitterId}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region TextureVelocityHook
    //: Entity.TextureVelocityHook
    public class TextureVelocityHook : AnimationHook, IHaveMetaInfo
    {
        public readonly float USpeed;
        public readonly float VSpeed;

        public TextureVelocityHook(AnimationHook hook) : base(hook) { }
        public TextureVelocityHook(BinaryReader r) : base(r)
        {
            USpeed = r.ReadSingle();
            VSpeed = r.ReadSingle();
        }

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is TextureVelocityHook s) nodes.Add(new MetaInfo($"USpeed: {s.USpeed}, VSpeed: {s.VSpeed}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region TextureVelocityPartHook
    //: Entity.TextureVelocityPartHook
    public class TextureVelocityPartHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint PartIndex;
        public readonly float USpeed;
        public readonly float VSpeed;

        public TextureVelocityPartHook(AnimationHook hook) : base(hook) { }
        public TextureVelocityPartHook(BinaryReader r) : base(r)
        {
            PartIndex = r.ReadUInt32();
            USpeed = r.ReadSingle();
            VSpeed = r.ReadSingle();
        }

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is TextureVelocityPartHook s)
            {
                nodes.Add(new($"PartIndex: {s.PartIndex}"));
                nodes.Add(new($"USpeed: {s.USpeed}, VSpeed: {s.VSpeed}"));
            }
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region TransparentHook
    //: Entity.TransparentHook
    public class TransparentHook : AnimationHook, IHaveMetaInfo
    {
        public readonly float Start;
        public readonly float End;
        public readonly float Time;

        public TransparentHook(AnimationHook hook) : base(hook) { }
        public TransparentHook(BinaryReader r) : base(r)
        {
            Start = r.ReadSingle();
            End = r.ReadSingle();
            Time = r.ReadSingle();
        }

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is TransparentHook s) nodes.Add(new MetaInfo($"Start: {s.Start}, End: {s.End}, Time: {s.Time}"));
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion

    #region TransparentPartHook
    //: Entity.TransparentPartHook
    public class TransparentPartHook : AnimationHook, IHaveMetaInfo
    {
        public readonly uint Part;
        public readonly float Start;
        public readonly float End;
        public readonly float Time;

        public TransparentPartHook(AnimationHook hook) : base(hook) { }
        public TransparentPartHook(BinaryReader r) : base(r)
        {
            Part = r.ReadUInt32();
            Start = r.ReadSingle();
            End = r.ReadSingle();
            Time = r.ReadSingle();
        }

        public override List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>();
            if (Base is TransparentPartHook s)
            {
                nodes.Add(new($"Part: {s.Part}"));
                nodes.Add(new($"Start: {s.Start}, End: {s.End}, Time: {s.Time}"));
            }
            nodes.AddRange(base.GetInfoNodes(resource, file, tag));
            return nodes;
        }
    }
    #endregion
}
