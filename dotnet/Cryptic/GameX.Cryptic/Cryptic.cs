using GameX.Cryptic.Formats;
using GameX.Cryptic.Transforms;
using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.Unknown;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GameX.Cryptic
{
    #region CrypticPakFile

    /// <summary>
    /// CrypticPakFile
    /// </summary>
    /// <seealso cref="GameX.Formats.BinaryPakFile" />
    public class CrypticPakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CrypticPakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public CrypticPakFile(PakState state) : base(state, GetPakBinary(state.Game, Path.GetExtension(state.Path).ToLowerInvariant()))
        {
            ObjectFactoryFunc = ObjectFactory;
        }

        #region Factories

        static PakBinary GetPakBinary(FamilyGame game, string extension)
            => PakBinary_Hogg.Current;

        //ref https://github.com/PlumberTaskForce/Datamining-Guide/blob/master/README.md
        internal static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => Path.GetExtension(source.Path).ToLowerInvariant() switch
            {
                ".bin" => (0, Binary_Bin.Factory),
                ".htex" or ".wtex" => (0, Binary_Tex.Factory), // Textures
                ".mset" => (0, Binary_MSet.Factory), // 3D Models
                ".fsb" => (0, Binary_Fsb.Factory), // FMod Soundbanks
                ".bik" => (0, Binary_Bik.Factory), // Bink Video
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