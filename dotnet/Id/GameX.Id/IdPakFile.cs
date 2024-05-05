using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.Id.Formats;
using GameX.Id.Transforms;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GameX.Id
{
    /// <summary>
    /// IdPakFile
    /// </summary>
    /// <seealso cref="GameX.Formats.BinaryPakFile" />
    public class IdPakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IdPakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public IdPakFile(PakState state) : base(state, GetPakBinary(state.Game, state.Path))
        {
            ObjectFactoryFactoryMethod = ObjectFactoryFactory;
        }

        #region Factories

        static PakBinary GetPakBinary(FamilyGame game, string filePath)
             => Path.GetExtension(filePath).ToLowerInvariant() switch
             {
                 "" => null,
                 var x when x == ".pk3" || x == ".pk4" || x == ".zip" => PakBinary_Zip.GetPakBinary(game),
                 ".pak" => PakBinary_Pak.Instance,
                 ".wad" => PakBinary_Wad.Instance,
                 _ => throw new ArgumentOutOfRangeException(),
             };

        public static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactoryFactory(FileSource source, FamilyGame game)
            => game.Id switch
            {
                //var x when x == "Q" || x == "Q2" || x == "Q3" || x == "D3" || x == "Q:L" => PakBinary_Pak.ObjectFactoryFactory(source, game),
                _ => PakBinary_Pak.ObjectFactoryFactory(source, game) // throw new ArgumentOutOfRangeException(),
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
}