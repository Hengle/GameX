using GameX.Bullfrog.Formats;
using GameX.Bullfrog.Transforms;
using GameX.Formats;
using GameX.Formats.Unknown;
using System;
using System.IO;
using System.Threading.Tasks;

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
            => ObjectFactoryFunc = ObjectFactoryFactory;

        #region Factories

        static PakBinary GetPakBinary(FamilyGame game, string filePath)
            => game.Id switch
            {
                var x when x == "DK" || x == "DK2" => PakBinary_Bullfrog.Instance, // Keeper
                var x when x == "P" || x == "P2" || x == "P3" => PakBinary_Populus.Instance,
                var x when x == "S" || x == "S2" => PakBinary_Bullfrog.Instance, // Syndicate
                var x when x == "MC" || x == "MC2" => PakBinary_Bullfrog.Instance, // Carpet
                var x when x == "TP" || x == "TH" => PakBinary_Bullfrog.Instance, // Theme
                _ => throw new ArgumentOutOfRangeException(),
            };

        static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactoryFactory(FileSource source, FamilyGame game)
            => game.Id switch
            {
                var x when x == "DK" || x == "DK2" => PakBinary_Bullfrog.ObjectFactoryFactory(source, game),
                var x when x == "P" || x == "P2" || x == "P3" => PakBinary_Populus.ObjectFactoryFactory(source, game),
                var x when x == "S" || x == "S2" => PakBinary_Bullfrog.ObjectFactoryFactory(source, game),
                _ => throw new ArgumentOutOfRangeException(),
                //_ => Path.GetExtension(source.Path).ToLowerInvariant() switch
                //{
                //    var x when x == ".cfg" || x == ".csv" || x == ".txt" => (0, Binary_Txt.Factory),
                //    var x when x == ".dat" => (0, Binary_Txt.Factory),
                //    _ => (0, null),
                //},
            };

        #endregion

        #region Transforms

        bool ITransformFileObject<IUnknownFileModel>.CanTransformFileObject(PakFile transformTo, object source) => UnknownTransform.CanTransformFileObject(this, transformTo, source);
        Task<IUnknownFileModel> ITransformFileObject<IUnknownFileModel>.TransformFileObject(PakFile transformTo, object source) => UnknownTransform.TransformFileObjectAsync(this, transformTo, source);

        #endregion
    }
}