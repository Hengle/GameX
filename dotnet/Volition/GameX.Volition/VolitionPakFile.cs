using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.Volition.Formats;
using GameX.Volition.Formats.Descent;
using GameX.Volition.Transforms;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace GameX.Volition
{
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
            ObjectFactoryFunc = ObjectFactoryFactory;
        }

        #region Factories

        static readonly ConcurrentDictionary<string, PakBinary> PakBinarys = new ConcurrentDictionary<string, PakBinary>();

        static PakBinary GetPakBinary(FamilyGame game, string extension)
            => PakBinarys.GetOrAdd(game.Id, _ => game.Engine switch
            {
                "Descent" => PakBinary_Descent.Instance,
                "CTG" => PakBinary_Ctg.Instance,
                "Geo-Mod" => PakBinary_GeoMod.Instance,
                "Geo-Mod2" => PakBinary_GeoMod.Instance,
                _ => throw new ArgumentOutOfRangeException(nameof(game.Engine)),
            });

        static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactoryFactory(FileSource source, FamilyGame game)
            => Path.GetExtension(source.Path).ToLowerInvariant() switch
            {
                var x when x == ".cfg" || x == ".csv" || x == ".txt" => (0, Binary_Txt.Factory),
                ".256" => (0, Binary_Pal.Factory_3),
                ".bmp" => (0, Binary_Bmp.Factory),
                ".wav" => (0, Binary_Snd.Factory),
                ".pcx" => (0, Binary_Pcx.Factory),
                ".mvl" => (0, Binary_Mvl.Factory),
                _ => (0, null),
            };

        #endregion

        #region Transforms

        bool ITransformFileObject<IUnknownFileModel>.CanTransformFileObject(PakFile transformTo, object source) => UnknownTransform.CanTransformFileObject(this, transformTo, source);
        Task<IUnknownFileModel> ITransformFileObject<IUnknownFileModel>.TransformFileObject(PakFile transformTo, object source) => UnknownTransform.TransformFileObjectAsync(this, transformTo, source);

        #endregion
    }
}