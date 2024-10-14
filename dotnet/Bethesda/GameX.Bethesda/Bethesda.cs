using GameX.Bethesda.Formats;
using GameX.Bethesda.Transforms;
using GameX.Formats;
using GameX.Formats.Unknown;
using GameX.Unknown;
using OpenStack.Gfx;
using OpenStack.Gfx.Textures;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using static OpenStack.Debug;

namespace GameX.Bethesda
{
    #region BethesdaFamily

    /// <summary>
    /// BethesdaFamily
    /// </summary>
    /// <seealso cref="GameX.Family" />
    public class BethesdaFamily(JsonElement elem) : Family(elem) { }

    #endregion

    #region BethesdaGame

    /// <summary>
    /// BethesdaGame
    /// </summary>
    /// <seealso cref="GameX.FamilyGame" />
    public class BethesdaGame(Family family, string id, JsonElement elem, FamilyGame dgame) : FamilyGame(family, id, elem, dgame)
    {
        /// <summary>
        /// Ensures this instance.
        /// </summary>
        /// <returns></returns>
        public override FamilyGame Ensure() => DatabaseManager.Ensure(this);
    }

    #endregion

    #region BethesdaPakFile

    /// <summary>
    /// BethesdaPakFile
    /// </summary>
    /// <seealso cref="GameX.Formats.BinaryPakFile" />
    public class BethesdaPakFile : BinaryPakFile, ITransformFileObject<IUnknownFileModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BethesdaPakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        public BethesdaPakFile(PakState state) : base(state, GetPakBinary(state.Game, Path.GetExtension(state.Path).ToLowerInvariant()))
        {
            ObjectFactoryFunc = ObjectFactory;
            PathFinders.Add(typeof(ITexture), FindTexture);
        }

        #region PathFinders

        /// <summary>
        /// Finds the actual path of a texture.
        /// </summary>
        public string FindTexture(string path)
        {
            var textureName = Path.GetFileNameWithoutExtension(path);
            var textureNameInTexturesDir = $"textures/{textureName}";
            var filePath = $"{textureNameInTexturesDir}.dds";
            if (Contains(filePath)) return filePath;
            //filePath = $"{textureNameInTexturesDir}.tga";
            //if (Contains(filePath)) return filePath;
            var texturePathWithoutExtension = $"{Path.GetDirectoryName(path)}/{textureName}";
            filePath = $"{texturePathWithoutExtension}.dds";
            if (Contains(filePath)) return filePath;
            //filePath = $"{texturePathWithoutExtension}.tga";
            //if (Contains(filePath)) return filePath;
            Log($"Could not find file '{path}' in a PAK file.");
            return null;
        }

        #endregion

        #region Factories

        static PakBinary GetPakBinary(FamilyGame game, string extension)
            => extension switch
            {
                "" => PakBinary_Bsa.Current,
                ".bsa" => PakBinary_Bsa.Current,
                ".ba2" => PakBinary_Ba2.Current,
                ".esm" => PakBinary_Esm.Current,
                _ => throw new ArgumentOutOfRangeException(nameof(extension)),
            };

        static Task<object> NiFactory(BinaryReader r, FileSource f, PakFile s) { var file = new NiFile(Path.GetFileNameWithoutExtension(f.Path)); file.Read(r); return Task.FromResult((object)file); }

        static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => Path.GetExtension(source.Path).ToLowerInvariant() switch
            {
                ".nif" => (0, NiFactory),
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