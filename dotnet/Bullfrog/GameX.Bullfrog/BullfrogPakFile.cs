using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.Bullfrog.Formats;
using GameX.Bullfrog.Transforms;
using System;
using System.IO;
using System.Threading.Tasks;
using static GameX.Formats.Unknown.IUnknownFileObject;

namespace GameX.Bullfrog
{
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
        {
            ObjectFactoryFactoryMethod = ObjectFactoryFactory;
        }

        #region Factories

        static PakBinary GetPakBinary(FamilyGame game, string filePath)
            => game.Id switch
            {
                "P" => PakBinary_Pop1.Instance,
                "P2" => PakBinary_Pop1.Instance,
                "DK" => PakBinary_Keeper.Instance,
                "P3" => PakBinary_Pop3.Instance,
                _ => throw new ArgumentOutOfRangeException(),
            };

        static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactoryFactory(FileSource source, FamilyGame game)
            => game.Id switch
            {
                "DK" => PakBinary_Keeper.ObjectFactoryFactory(source, game),
                _ => Path.GetExtension(source.Path).ToLowerInvariant() switch
                {
                    var x when x == ".cfg" || x == ".csv" || x == ".txt" => (0, Binary_Txt.Factory),
                    var x when x == ".dat" => (0, Binary_Txt.Factory),
                    _ => (0, null),
                },
            };

        #endregion

        #region Transforms

        bool ITransformFileObject<IUnknownFileModel>.CanTransformFileObject(PakFile transformTo, object source) => UnknownTransform.CanTransformFileObject(this, transformTo, source);
        Task<IUnknownFileModel> ITransformFileObject<IUnknownFileModel>.TransformFileObject(PakFile transformTo, object source) => UnknownTransform.TransformFileObjectAsync(this, transformTo, source);

        #endregion
    }
}