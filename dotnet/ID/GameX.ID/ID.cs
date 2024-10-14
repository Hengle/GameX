using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.ID.Formats;
using GameX.ID.Transforms;
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
                 var x when x == ".pk3" || x == ".pk4" || x == ".zip" => PakBinary_Zip.GetPakBinary(game),
                 ".pak" => PakBinary_Pak.Current,
                 ".wad" => PakBinary_Wad.Current,
                 _ => throw new ArgumentOutOfRangeException(),
             };

        public static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => game.Id switch
            {
                //var x when x == "Q" || x == "Q2" || x == "Q3" || x == "D3" || x == "Q:L" => PakBinary_Pak.ObjectFactory(source, game),
                _ => PakBinary_Pak.ObjectFactory(source, game) // throw new ArgumentOutOfRangeException(),
            };
        //=> Path.GetExtension(source.Path).ToLowerInvariant() switch
        //{
        //    ".wav" => (0, Binary_Snd.Factory),
        //    ".dds" => (0, Binary_Dds.Factory),
        //    _ => (0, null),
        //};

        #endregion

        #region Transforms

        bool ITransformFileObject<IUnknownFileModel>.CanTransformFileObject(PakFile transformTo, object source) => UnknownTransform.CanTransformFileObject(this, transformTo, source);
        Task<IUnknownFileModel> ITransformFileObject<IUnknownFileModel>.TransformFileObject(PakFile transformTo, object source) => UnknownTransform.TransformFileObjectAsync(this, transformTo, source);

        #endregion
    }

    #endregion
}