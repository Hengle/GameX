using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.Valve.Formats;
using GameX.Valve.Formats.Vpk;
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
            ObjectFactoryFunc = ObjectFactoryFactory;
            PathFinders.Add(typeof(object), FindBinary);
        }

        #region Factories

        static readonly ConcurrentDictionary<string, PakBinary> PakBinarys = new();

        static PakBinary GetPakBinary(FamilyGame game, string extension)
            => PakBinarys.GetOrAdd(game.Id, _ => game.Engine switch
            {
                "Unity" => Unity.Formats.PakBinary_Unity.Current,
                "GoldSrc" => PakBinary_Wad.Current,
                "Source" => PakBinary_Vpk.Current,
                _ => throw new ArgumentOutOfRangeException(nameof(game.Engine), game.Engine),
            });

        public static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactoryFactory(FileSource source, FamilyGame game)
        {
            return game.Engine switch
            {
                "GoldSrc" => Path.GetExtension(source.Path).ToLowerInvariant() switch
                {
                    var x when x == ".txt" || x == ".ini" || x == ".asl" => (0, Binary_Txt.Factory),
                    ".wav" => (0, Binary_Snd.Factory),
                    var x when x == ".bmp" || x == ".jpg" => (0, Binary_Img.Factory),
                    var x when x == ".pic" || x == ".tex" || x == ".tex2" || x == ".fnt" => (0, Binary_Wad3.Factory),
                    ".bsp" => (0, Binary_Bsp.Factory),
                    ".spr" => (0, Binary_Spr.Factory),
                    _ => default,
                },
                "Source" => (0, Binary_Src.Factory),
                _ => throw new ArgumentOutOfRangeException(nameof(game.Engine), game.Engine),
            };
        }

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