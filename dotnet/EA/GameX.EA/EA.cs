using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.EA.Formats;
using GameX.EA.Transforms;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GameX.EA
{
    #region EAPakFile

    /// <summary>
    /// EAPakFile
    /// </summary>
    /// <seealso cref="GameX.Formats.BinaryPakFile" />
    public class EAPakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EAPakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public EAPakFile(PakState state) : base(state, PakBinary_Hpl.Current)
        {
            ObjectFactoryFunc = ObjectFactory;
        }

        #region Factories

        static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => Path.GetExtension(source.Path).ToLowerInvariant() switch
            {
                var x when x == ".cfg" || x == ".csv" || x == ".txt" => (0, Binary_Txt.Factory),
                _ => (0, null),
            };

        #endregion

        #region Transforms

        bool ITransformFileObject<IUnknownFileModel>.CanTransformFileObject(PakFile transformTo, object source) => UnknownTransform.CanTransformFileObject(this, transformTo, source);
        Task<IUnknownFileModel> ITransformFileObject<IUnknownFileModel>.TransformFileObject(PakFile transformTo, object source) => UnknownTransform.TransformFileObjectAsync(this, transformTo, source);

        #endregion
    }

    #endregion
}