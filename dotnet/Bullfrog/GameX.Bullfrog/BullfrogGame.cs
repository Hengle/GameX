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
        public override FamilyGame Ensure() => Id switch
        {
            "DK" => Games.DK.Database.Ensure(this),
            "DK2" => Games.DK2.Database.Ensure(this),
            "P2" => Games.P2.Database.Ensure(this),
            "S" => Games.S.Database.Ensure(this),
            _ => this,
        };
    }
}