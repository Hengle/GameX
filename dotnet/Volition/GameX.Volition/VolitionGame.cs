using System.Text.Json;

namespace GameX.Volition
{
    /// <summary>
    /// VolitionGame
    /// </summary>
    /// <seealso cref="GameX.FamilyGame" />
    public class VolitionGame : FamilyGame
    {
        public VolitionGame(Family family, string id, JsonElement elem, FamilyGame dgame) : base(family, id, elem, dgame) { }

        /// <summary>
        /// Ensures this instance.
        /// </summary>
        /// <returns></returns>
        public override FamilyGame Ensure()
        {
            switch (Id)
            {
                case "D": Games.D.Database.Ensure(this); return this;
                case "D2": Games.D2.Database.Ensure(this); return this;
                default: return this;
            }
        }
    }
}