using GameX.Arkane.Formats;
using GameX.Arkane.Formats.Danae;
using GameX.Arkane.Transforms;
using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.Unknown;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace GameX.Arkane
{
    #region ArkanePakFile

    /// <summary>
    /// ArkanePakFile
    /// </summary>
    /// <seealso cref="GameEstate.Formats.BinaryPakFile" />
    public class ArkanePakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Arkane" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public ArkanePakFile(PakState state) : base(state, GetPakBinary(state.Game, Path.GetExtension(state.Path).ToLowerInvariant()))
        {
            ObjectFactoryFunc = state.Game.Engine.n switch
            {
                "CryEngine" => Crytek.CrytekPakFile.ObjectFactory,
                "Unreal" => Epic.EpicPakFile.ObjectFactory,
                "Source" => Valve.ValvePakFile.ObjectFactory,
                "idTech" => ID.IDPakFile.ObjectFactory,
                _ => ObjectFactory,
            };
            UseFileId = true;
        }

        #region Factories

        static readonly ConcurrentDictionary<string, PakBinary> PakBinarys = new();

        static PakBinary GetPakBinary(FamilyGame game, string extension)
            => PakBinarys.GetOrAdd(game.Id, _ => game.Engine.n switch
            {
                "Danae" => PakBinary_Danae.Current,
                "Void" => PakBinary_Void.Current,
                "CryEngine" => Crytek.Formats.PakBinary_Cry3.Current,
                "Unreal" => Epic.Formats.PakBinary_Pck.Current,
                "Source" => Valve.Formats.PakBinary_Vpk.Current,
                "idTech" => ID.Formats.PakBinary_Pak.Current,
                _ => throw new ArgumentOutOfRangeException(nameof(game.Engine)),
            });

        internal static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => Path.GetExtension(source.Path).ToLowerInvariant() switch
            {
                ".asl" => (0, Binary_Txt.Factory),
                // Danae (AF)
                ".ftl" => (0, Binary_Ftl.Factory),
                ".fts" => (0, Binary_Fts.Factory),
                ".tea" => (0, Binary_Tea.Factory),
                //
                //".llf" => (0, Binary_Flt.Factory),
                //".dlf" => (0, Binary_Flt.Factory),
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