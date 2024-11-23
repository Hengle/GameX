using GameX.ID.Formats;
using System;

namespace GameX.ID.Games.Q
{
    public static class Database
    {
        public static PakFile PakFile;

        internal static FamilyGame Ensure(FamilyGame game)
        {
            PakFile = game.Family.OpenPakFile(new Uri("game:/#Q"));
            PakFile.LoadFileObject<Binary_Lmp>("PAK0.PAK:gfx/palette.lmp");
            PakFile.LoadFileObject<Binary_Lmp>("PAK0.PAK:gfx/colormap.lmp");
            return game;
        }
    }
}
