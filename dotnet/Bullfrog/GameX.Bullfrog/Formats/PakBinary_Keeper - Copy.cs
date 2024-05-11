using GameX.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GameX.Bullfrog.Formats
{
    public unsafe class PakBinary_Keeper2
    {
        /*
        enum FGRP
        {
            None,
            StdData,
            LrgData,
            FxData,
            LoData,
            HiData,
            VarLevels,
            Save,
            SShots,
            StdSound,
            LrgSound,
            AtlSound,
            Main,
            Campgn,
            CmpgLvls,
            LandView,
            CrtrData,
            CmpgCrtrs,
            CmpgConfig,
            CmpgMedia,
            Music,
        }

        static string GetPath(FGRP fgroup, string fname, string run_path, string inst_path)
        {
            string mdir = null, sdir = null;
            switch (fgroup)
            {
                case FGRP.StdData: mdir = run_path; sdir = "data"; break;
                case FGRP.LrgData: mdir = run_path; sdir = "data"; break;
                case FGRP.FxData: mdir = run_path; sdir = "fxdata"; break;
                case FGRP.LoData: mdir = inst_path; sdir = "ldata"; break;
                case FGRP.HiData: mdir = run_path; sdir = "hdata"; break;
                case FGRP.Music: mdir = run_path; sdir = "music"; break;
                case FGRP.VarLevels: mdir = inst_path; sdir = "levels"; break;
                case FGRP.Save: mdir = run_path; sdir = "save"; break;
                case FGRP.SShots: mdir = run_path; sdir = "scrshots"; break;
                case FGRP.StdSound: mdir = run_path; sdir = "sound"; break;
                case FGRP.LrgSound: mdir = run_path; sdir = "sound"; break;
                case FGRP.AtlSound:
                    if (campaign.speech_location == null) break;
                    mdir = run_path; sdir = campaign.speech_location; break;
                case FGRP.Main: mdir = run_path; sdir = null; break;
                case FGRP.Campgn: mdir = run_path; sdir = "campgns"; break;
                case FGRP.CmpgLvls:
                    if (campaign.levels_location != null) break;
                    mdir = inst_path; sdir = campaign.levels_location; break;
                case FGRP.CmpgCrtrs:
                    if (campaign.creatures_location == null) break;
                    mdir = inst_path; sdir = campaign.creatures_location; break;
                case FGRP.CmpgConfig:
                    if (campaign.configs_location == null) break;
                    mdir = inst_path; sdir = campaign.configs_location; break;
                case FGRP.CmpgMedia:
                    if (campaign.media_location == null) break;
                    mdir = inst_path; sdir = campaign.media_location; break;
                case FGRP.LandView:
                    if (campaign.land_location == null) break;
                    mdir = inst_path; sdir = campaign.land_location; break;
                case FGRP.CrtrData: mdir = run_path; sdir = "creatrs"; break;
                default: mdir = "./"; sdir = null; break;
            }
            return mdir == null ? string.Empty
                : sdir == null ? $"{mdir}/{fname}"
                : $"{mdir}/{sdir}/{fname}";
        }
        */
    }
}