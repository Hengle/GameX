using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.IW.Formats;
using GameX.IW.Transforms;
using GameX.Unknown;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GameX.IW
{
    #region IWPakFile

    /// <summary>
    /// IWPakFile
    /// </summary>
    /// <seealso cref="GameX.Formats.BinaryPakFile" />
    public class IWPakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IWPakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public IWPakFile(PakState state) : base(state, PakBinary_IW.Current)
        {
            ObjectFactoryFunc = ObjectFactory;
            UseReader = false;
        }

        #region Factories

        static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => Path.GetExtension(source.Path).ToLowerInvariant() switch
            {
                //".roq" => (0, VIDEO.Factory),
                //".wav" => (0, BinaryWav.Factory),
                //".d3dbsp" => (0, BinaryD3dbsp.Factory),
                ".iwi" => (0, Binary_Iwi.Factory),
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