using GameX.Formats;
using System;

namespace GameX.Volition.Games.D
{
    public static class Database
    {
        public static PakFile PakFile;
        public static Binary_Pal Palette;

        internal static FamilyGame Ensure(FamilyGame game)
        {
            PakFile = game.Family.OpenPakFile(new Uri("game:/descent.hog#D"));
            Palette = PakFile.LoadFileObject<Binary_Pal>("palette.256").Result.ConvertVgaPalette();
            return game;
        }
    }
}
