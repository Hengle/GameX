﻿using GameX.Epic.Formats;
using GameX.Epic.Transforms;
using GameX.Formats;
using GameX.Formats.Unknown;
using System.IO;
using System;
using System.Threading.Tasks;
using GameX.Unknown;

namespace GameX.Epic
{
    #region EpicPakFile

    /// <summary>
    /// EpicPakFile
    /// </summary>
    /// <seealso cref="GameX.Formats.BinaryPakFile" />
    public class EpicPakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EpicPakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public EpicPakFile(PakState state) : base(state, PakBinary_Pck.Current)
        {
            ObjectFactoryFunc = ObjectFactory;
        }

        #region Factories

        // object factory
        public static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => Path.GetExtension(source.Path).ToLowerInvariant() switch
            {
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