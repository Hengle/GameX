using System.Text.Json;

namespace GameX.WB
{
    /// <summary>
    /// WBGame
    /// </summary>
    /// <seealso cref="GameX.FamilyGame" />
    public class WBGame : FamilyGame
    {
        public WBGame(Family family, string id, JsonElement elem, FamilyGame dgame) : base(family, id, elem, dgame) { }

        /// <summary>
        /// Ensures this instance.
        /// </summary>
        /// <returns></returns>
        public override FamilyGame Ensure() => DatabaseManager.Ensure(this);
    }
}