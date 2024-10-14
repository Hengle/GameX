using GameX.Formats.Unknown;
using GameX.Origin.Formats;
using GameX.Origin.Transforms;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameX.Origin
{
    #region OriginGame

    /// <summary>
    /// OriginGame
    /// </summary>
    /// <seealso cref="GameX.FamilyGame" />
    public class OriginGame(Family family, string id, JsonElement elem, FamilyGame dgame) : FamilyGame(family, id, elem, dgame)
    {
        /// <summary>
        /// Ensures this instance.
        /// </summary>
        /// <returns></returns>
        public override FamilyGame Ensure()
        {
            switch (Id)
            {
                case "U9": Games.U9.Database.Ensure(this); return this;
                //case "U8": Structs.UO.Database.Ensure(this); return this;
                default: return this;
            }
        }
    }

    #endregion

    #region OriginPakFile

    /// <summary>
    /// OriginPakFile
    /// </summary>
    /// <seealso cref="GameX.Formats.BinaryPakFile" />
    public class OriginPakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OriginPakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public OriginPakFile(PakState state) : base(state, GetPakBinary(state.Game))
        {
            ObjectFactoryFunc = ObjectFactory;
        }

        #region Factories

        static PakBinary GetPakBinary(FamilyGame game)
            => game.Id switch
            {
                "U8" => PakBinary_U8.Current,
                "UO" => PakBinary_UO.Current,
                "U9" => PakBinary_U9.Current,
                _ => throw new ArgumentOutOfRangeException(),
            };

        static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => game.Id switch
            {
                "U8" => PakBinary_U8.ObjectFactory(source, game),
                "UO" => PakBinary_UO.ObjectFactory(source, game),
                "U9" => PakBinary_U9.ObjectFactory(source, game),
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