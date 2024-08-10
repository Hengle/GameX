using System.Text.Json;

namespace GameX.ID
{
    /// <summary>
    /// IDGame
    /// </summary>
    /// <seealso cref="GameX.FamilyGame" />
    public class IDGame : FamilyGame
    {
        public IDGame(Family family, string id, JsonElement elem, FamilyGame dgame) : base(family, id, elem, dgame) { }

        /// <summary>
        /// Ensures this instance.
        /// </summary>
        /// <returns></returns>
        public override FamilyGame Ensure()
        {
            switch (Id)
            {
                case "Q": Games.Q.Database.Ensure(this); return this;
                default: return this;
            }
        }
    }
}