using GameX.Formats.Unknown;
using GameX.Unknown;
using GameX.Valve.Formats;
using GameX.Valve.Transforms;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using static OpenStack.Debug;

namespace GameX.Valve
{
    #region ValvePakFile

    /// <summary>
    /// ValvePakFile
    /// </summary>
    /// <seealso cref="GameX.Formats.BinaryPakFile" />
    public class ValvePakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValvePakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public ValvePakFile(PakState state) : base(state, GetPakBinary(state.Game, Path.GetExtension(state.Path).ToLowerInvariant()))
        {
            ObjectFactoryFunc = ObjectFactory;
            PathFinders.Add(typeof(object), FindBinary);
        }

        #region Factories

        static readonly ConcurrentDictionary<string, PakBinary> PakBinarys = new();

        static PakBinary GetPakBinary(FamilyGame game, string extension)
            => extension != ".bsp"
                ? PakBinarys.GetOrAdd(game.Id, _ => game.Engine switch
                {
                    "Unity" => Unity.Formats.PakBinary_Unity.Current,
                    "GoldSrc" => PakBinary_Wad3.Current,
                    "Source" or "Source2" => PakBinary_Vpk.Current,
                    _ => throw new ArgumentOutOfRangeException(nameof(game.Engine), game.Engine),
                })
                : PakBinary_Bsp30.Current;

        public static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => game.Engine switch
            {
                "GoldSrc" => Path.GetExtension(source.Path).ToLowerInvariant() switch
                {
                    ".pic" or ".tex" or ".tex2" or ".fnt" => (0, Binary_Wad3.Factory),
                    ".spr" => (0, Binary_Spr.Factory),
                    ".mdl" => (0, Binary_Mdl10.Factory),
                    _ => UnknownPakFile.ObjectFactory(source, game),
                },
                "Source" => Path.GetExtension(source.Path).ToLowerInvariant() switch
                {
                    ".mdl" => (0, Binary_Mdl40.Factory),
                    _ => UnknownPakFile.ObjectFactory(source, game),
                },
                "Source2" => (0, Binary_Src.Factory),
                _ => throw new ArgumentOutOfRangeException(nameof(game.Engine), game.Engine),
            };

        #endregion

        #region PathFinders

        /// <summary>
        /// Finds the actual path of a texture.
        /// </summary>
        public string FindBinary(string path)
        {
            if (Contains(path)) return path;
            if (!path.EndsWith("_c", StringComparison.Ordinal)) path = $"{path}_c";
            if (Contains(path)) return path;
            Log($"Could not find file '{path}' in a PAK file.");
            return null;
        }

        #endregion

        #region Transforms

        bool ITransformFileObject<IUnknownFileModel>.CanTransformFileObject(PakFile transformTo, object source) => UnknownTransform.CanTransformFileObject(this, transformTo, source);
        Task<IUnknownFileModel> ITransformFileObject<IUnknownFileModel>.TransformFileObject(PakFile transformTo, object source) => UnknownTransform.TransformFileObjectAsync(this, transformTo, source);

        #endregion
    }

    #endregion
}