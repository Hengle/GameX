using System.Text.Json;

namespace GameX.Bullfrog
{
    /// <summary>
    /// BullfrogGame
    /// </summary>
    /// <seealso cref="GameX.FamilyGame" />
    public class BullfrogGame : FamilyGame
    {
        public BullfrogGame(Family family, string id, JsonElement elem, FamilyGame dgame) : base(family, id, elem, dgame) { }

        /// <summary>
        /// Ensures this instance.
        /// </summary>
        /// <returns></returns>
        public override FamilyGame Ensure()
        {
            switch (Id)
            {
                case "DK": Games.DK.Database.Ensure(this); return this;
                default: return this;
            }
        }
    }
}