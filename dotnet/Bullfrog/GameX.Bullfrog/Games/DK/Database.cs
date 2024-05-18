using GameX.Formats;
using System;

namespace GameX.Bullfrog.Games.DK
{
    public static class Database
    {
        public static PakFile PakFile;
        public static Binary_Pal Palette;

        internal static FamilyGame Ensure(FamilyGame game)
        {
            PakFile = game.Family.OpenPakFile(new Uri("game:/#DK"));
            Palette = PakFile.LoadFileObject<Binary_Pal>("DATA/MAIN.PAL").Result;
            return game;
        }
    }
}
