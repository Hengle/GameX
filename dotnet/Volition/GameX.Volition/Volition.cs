using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.Unknown;
using GameX.Volition.Formats;
using GameX.Volition.Formats.Descent;
using GameX.Volition.Transforms;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameX.Volition
{
    #region VolitionGame

    /// <summary>
    /// VolitionGame
    /// </summary>
    /// <seealso cref="GameX.FamilyGame" />
    public class VolitionGame(Family family, string id, JsonElement elem, FamilyGame dgame) : FamilyGame(family, id, elem, dgame)
    {
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

    #endregion

    #region VolitionPakFile

    /// <summary>
    /// VolitionPakFile
    /// </summary>
    /// <seealso cref="GameX.Formats.BinaryPakFile" />
    public class VolitionPakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VolitionPakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public VolitionPakFile(PakState state) : base(state, GetPakBinary(state.Game, Path.GetExtension(state.Path).ToLowerInvariant()))
        {
            ObjectFactoryFunc = ObjectFactory;
        }

        #region Factories

        static readonly ConcurrentDictionary<string, PakBinary> PakBinarys = new ConcurrentDictionary<string, PakBinary>();

        static PakBinary GetPakBinary(FamilyGame game, string extension)
            => PakBinarys.GetOrAdd(game.Id, _ => game.Engine.n switch
            {
                "Descent" => PakBinary_Descent.Current,
                "CTG" => PakBinary_Ctg.Current,
                "Geo-Mod" => PakBinary_GeoMod.Current,
                "Geo-Mod2" => PakBinary_GeoMod.Current,
                _ => throw new ArgumentOutOfRangeException(nameof(game.Engine.n)),
            });

        static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => Path.GetExtension(source.Path).ToLowerInvariant() switch
            {
                ".256" => (0, Binary_Pal.Factory_3),
                ".mvl" => (0, Binary_Mvl.Factory),
                _ => UnknownPakFile.ObjectFactory(source, game),
            };

        #endregion

        #region Transforms

        bool ITransformFileObject<IUnknownFileModel>.CanTransformFileObject(PakFile transformTo, object source) => UnknownTransform.CanTransformFileObject(this, transformTo, source);
        Task<IUnknownFileModel> ITransformFileObject<IUnknownFileModel>.TransformFileObject(PakFile transformTo, object source) => UnknownTransform.TransformFileObjectAsync(this, transformTo, source);

        #endregion
    }

    #endregion
}