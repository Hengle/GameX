using GameX.Cig.Formats;
using GameX.Cig.Transforms;
using GameX.Crytek.Formats;
using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.Unknown;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GameX.Cig
{
    #region CigPakFile

    /// <summary>
    /// CigPakFile
    /// </summary>
    /// <seealso cref="GameEstate.Formats.BinaryPakFile" />
    public class CigPakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CigPakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public CigPakFile(PakState state) : base(state, PakBinary_P4k.Current)
        {
            ObjectFactoryFunc = ObjectFactory;
        }

        #region Factories

        internal static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => Path.GetExtension(source.Path).ToLowerInvariant() switch
            {
                //".cfg" => (0, BinaryDcb.Factory),
                ".mtl" or ".xml" => (FileOption.Stream, CryXmlFile.Factory),
                ".a" => (0, Binary_DdsA.Factory),
                ".dcb" => (0, Binary_Dcb.Factory),
                ".soc" or ".cgf" or ".cga" or ".chr" or ".skin" or ".anim" => (FileOption.Model, CryFile.Factory),
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