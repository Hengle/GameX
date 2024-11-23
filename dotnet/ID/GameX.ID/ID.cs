using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.ID.Formats;
using GameX.ID.Transforms;
using GameX.Unknown;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameX.ID
{
    #region IDGame

    /// <summary>
    /// IDGame
    /// </summary>
    /// <seealso cref="GameX.FamilyGame" />
    public class IDGame(Family family, string id, JsonElement elem, FamilyGame dgame) : FamilyGame(family, id, elem, dgame)
    {
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

    #endregion

    #region IDPakFile

    /// <summary>
    /// IDPakFile
    /// </summary>
    /// <seealso cref="GameX.Formats.BinaryPakFile" />
    public class IDPakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IDPakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public IDPakFile(PakState state) : base(state, GetPakBinary(state.Game, state.Path))
        {
            ObjectFactoryFunc = ObjectFactory;
        }

        #region Factories

        static PakBinary GetPakBinary(FamilyGame game, string filePath)
             => Path.GetExtension(filePath).ToLowerInvariant() switch
             {
                 "" => null,
                 ".pk3" or ".pk4" or ".zip" => PakBinary_Zip.GetPakBinary(game),
                 //".bsp" => PakBinary_Bsp.Current,
                 ".pak" => PakBinary_Pak.Current,
                 ".wad" => PakBinary_Wad.Current,
                 _ => throw new ArgumentOutOfRangeException(),
             };

        public static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => game.Engine.n switch
            {
                "idTech" => Path.GetExtension(source.Path).ToLowerInvariant() switch
                {
                    ".lmp" or ".tex" => (0, Binary_Lmp.Factory),
                    ".bsp" => (0, Binary_Bsp.Factory),
                    ".mdl" => (0, Binary_Mdl.Factory),
                    ".spr" => (0, Binary_Spr.Factory),
                    _ => UnknownPakFile.ObjectFactory(source, game),
                },
                _ => throw new ArgumentOutOfRangeException(nameof(game.Engine), game.Engine.n),
            };

        #endregion

        #region Transforms

        bool ITransformFileObject<IUnknownFileModel>.CanTransformFileObject(PakFile transformTo, object source) => UnknownTransform.CanTransformFileObject(this, transformTo, source);
        Task<IUnknownFileModel> ITransformFileObject<IUnknownFileModel>.TransformFileObject(PakFile transformTo, object source) => UnknownTransform.TransformFileObjectAsync(this, transformTo, source);

        #endregion
    }

    #endregion
}