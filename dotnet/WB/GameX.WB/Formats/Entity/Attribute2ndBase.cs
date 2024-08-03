using System.IO;

namespace GameX.WB.Formats.Entity
{
    public class Attribute2ndBase
    {
        public readonly SkillFormula Formula;

        public Attribute2ndBase(BinaryReader r)
            => Formula = new SkillFormula(r);
    }
}
