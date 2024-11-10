using GameX.Bioware.Formats;
using GameX.Bioware.Transforms;
using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.Unknown;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace GameX.Bioware
{
    #region BiowarePakFile

    /// <summary>
    /// BiowarePakFile
    /// </summary>
    /// <seealso cref="GameX.Formats.BinaryPakFile" />
    public class BiowarePakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BiowarePakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public BiowarePakFile(PakState state) : base(state, GetPakBinary(state.Game, Path.GetExtension(state.Path).ToLowerInvariant()))
        {
            ObjectFactoryFunc = ObjectFactory;
        }

        #region Factories

        static readonly ConcurrentDictionary<string, PakBinary> PakBinarys = new ConcurrentDictionary<string, PakBinary>();

        static PakBinary GetPakBinary(FamilyGame game, string extension)
            => extension != ".zip"
                ? PakBinarys.GetOrAdd(game.Id, _ => PakBinaryFactory(game))
                : PakBinary_Zip.GetPakBinary(game);

        static PakBinary PakBinaryFactory(FamilyGame game)
            => game.Engine switch
            {
                //"Infinity" => PakBinary_Infinity.Instance,
                "Aurora" => PakBinary_Aurora.Current,
                "HeroEngine" => PakBinary_Myp.Current,
                _ => throw new ArgumentOutOfRangeException(nameof(game.Engine))
            };

        static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => Path.GetExtension(source.Path).ToLowerInvariant() switch
            {
                ".dlg" or ".qdb" or ".qst" => (0, Binary_Gff.Factory),
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