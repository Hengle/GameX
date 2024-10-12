using GameX.Bullfrog.Formats;
using GameX.Bullfrog.Transforms;
using GameX.Formats.Unknown;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameX.Bullfrog
{
    #region BullfrogGame

    /// <summary>
    /// BullfrogGame
    /// </summary>
    /// <seealso cref="GameX.FamilyGame" />
    public class BullfrogGame(Family family, string id, JsonElement elem, FamilyGame dgame) : FamilyGame(family, id, elem, dgame)
    {
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

    #endregion

    #region BullfrogPakFile

    /// <summary>
    /// BullfrogPakFile
    /// </summary>
    /// <seealso cref="GameX.Formats.BinaryPakFile" />
    public class BullfrogPakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BullfrogPakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public BullfrogPakFile(PakState state) : base(state, GetPakBinary(state.Game, state.Path))
            => ObjectFactoryFunc = ObjectFactoryFactory;

        #region Factories

        static PakBinary GetPakBinary(FamilyGame game, string filePath)
            => game.Id switch
            {
                var x when x == "DK" || x == "DK2" => PakBinary_Bullfrog.Current,           // Keeper
                var x when x == "P" || x == "P2" || x == "P3" => PakBinary_Populus.Current, // Populus
                var x when x == "S" || x == "S2" => PakBinary_Syndicate.Current,             // Syndicate
                var x when x == "MC" || x == "MC2" => PakBinary_Bullfrog.Current,           // Carpet
                var x when x == "TP" || x == "TH" => PakBinary_Bullfrog.Current,            // Theme
                _ => throw new ArgumentOutOfRangeException(),
            };

        static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactoryFactory(FileSource source, FamilyGame game)
            => game.Id switch
            {
                var x when x == "DK" || x == "DK2" => PakBinary_Bullfrog.ObjectFactoryFactory(source, game),
                var x when x == "P" || x == "P2" || x == "P3" => PakBinary_Populus.ObjectFactoryFactory(source, game),
                var x when x == "S" || x == "S2" => PakBinary_Syndicate.ObjectFactoryFactory(source, game),
                _ => throw new ArgumentOutOfRangeException(),
            };

        #endregion

        #region Transforms

        bool ITransformFileObject<IUnknownFileModel>.CanTransformFileObject(PakFile transformTo, object source) => UnknownTransform.CanTransformFileObject(this, transformTo, source);
        Task<IUnknownFileModel> ITransformFileObject<IUnknownFileModel>.TransformFileObject(PakFile transformTo, object source) => UnknownTransform.TransformFileObjectAsync(this, transformTo, source);

        #endregion
    }

    #endregion
}