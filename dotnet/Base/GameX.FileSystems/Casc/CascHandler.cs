using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameX.FileSystems.Casc
{
    public sealed class CascHandler : CASCHandlerBase
    {
        private EncodingHandler EncodingHandler;
        private DownloadHandler DownloadHandler;
        private RootHandlerBase RootHandler;
        private InstallHandler InstallHandler;

        public EncodingHandler Encoding => EncodingHandler;
        public DownloadHandler Download => DownloadHandler;
        public RootHandlerBase Root => RootHandler;
        public InstallHandler Install => InstallHandler;

        private CascHandler(CascConfig config, BackgroundWorkerEx worker) : base(config, worker)
        {
            Logger.WriteLine("CASCHandler: loading encoding data...");

            using (var _ = new PerfCounter("new EncodingHandler()"))
            {
                using (var fs = OpenEncodingFile(this))
                    EncodingHandler = new EncodingHandler(fs, worker);
            }

            Logger.WriteLine("CASCHandler: loaded {0} encoding data", EncodingHandler.Count);

            if ((CascConfig.LoadFlags & LoadFlags.Download) != 0)
            {
                Logger.WriteLine("CASCHandler: loading download data...");

                using (var _ = new PerfCounter("new DownloadHandler()"))
                {
                    using (var fs = OpenDownloadFile(EncodingHandler, this))
                        DownloadHandler = new DownloadHandler(fs, worker);
                }

                Logger.WriteLine("CASCHandler: loaded {0} download data", EncodingHandler.Count);
            }

            KeyService.LoadKeys();

            Logger.WriteLine("CASCHandler: loading root data...");

            using (var _ = new PerfCounter("new RootHandler()"))
            {
                if (config.IsVfsRoot && (config.GameType != CascGameType.WoW || (config.GameType == CascGameType.WoW && CascConfig.UseWowTVfs)))
                {
                    if (config.GameType == CascGameType.D4)
                        RootHandler = new D4RootHandler(worker, this);
                    else if (config.GameType == CascGameType.WoW)
                        RootHandler = new WowTVFSRootHandler(worker, this);
                    else
                        RootHandler = new TVFSRootHandler(worker, this);
                }
                else
                {
                    using (var fs = OpenRootFile(EncodingHandler, this))
                    {
                        RootHandlerBase UnknownRootHandler()
                        {
                            using (var ufs = new FileStream("unk_root", FileMode.Create))
                                fs.BaseStream.CopyTo(ufs);
                            throw new Exception("Unsupported game " + config.BuildProduct);
                        }

                        RootHandler = config.GameType switch
                        {
                            CascGameType.S2 => new MNDXRootHandler(fs, worker),
                            CascGameType.HotS => new MNDXRootHandler(fs, worker),
                            CascGameType.D3 => new D3RootHandler(fs, worker, this),
                            CascGameType.WoW => new WowRootHandler(fs, worker),
                            CascGameType.S1 => new S1RootHandler(fs, worker),
                            CascGameType.Agent => new DummyRootHandler(fs, worker),
                            CascGameType.Bna => new DummyRootHandler(fs, worker),
                            CascGameType.Client => new DummyRootHandler(fs, worker),
                            CascGameType.Hearthstone => new DummyRootHandler(fs, worker),
                            CascGameType.Destiny2 => new DummyRootHandler(fs, worker),
                            CascGameType.Wlby => new DummyRootHandler(fs, worker),
                            CascGameType.Rtro => new DummyRootHandler(fs, worker),
                            CascGameType.Anbs => new DummyRootHandler(fs, worker),
                            CascGameType.WC1 => new DummyRootHandler(fs, worker),
                            CascGameType.WC2 => new DummyRootHandler(fs, worker),
                            CascGameType.DRTL => new DummyRootHandler(fs, worker),
                            CascGameType.DRTL2 => new DummyRootHandler(fs, worker),
                            CascGameType.Gryphon => new DummyRootHandler(fs, worker),
                            _ => UnknownRootHandler()
                        };
                    }
                }
            }

            Logger.WriteLine("CASCHandler: loaded {0} root data", RootHandler.Count);

            if ((CascConfig.LoadFlags & LoadFlags.Install) != 0)
            {
                Logger.WriteLine("CASCHandler: loading install data...");

                using (var _ = new PerfCounter("new InstallHandler()"))
                {
                    using (var fs = OpenInstallFile(EncodingHandler, this))
                        InstallHandler = new InstallHandler(fs, worker);

                    //InstallHandler.Print();
                }

                Logger.WriteLine("CASCHandler: loaded {0} install data", InstallHandler.Count);
            }
        }

        public static CascHandler OpenStorage(CascConfig config, BackgroundWorkerEx worker = null) => Open(config, worker);

        public static CascHandler OpenLocalStorage(string basePath, string product = null, BackgroundWorkerEx worker = null)
        {
            CascConfig config = CascConfig.LoadLocalStorageConfig(basePath, product);

            return Open(config, worker);
        }

        public static CascHandler OpenOnlineStorage(string product, string region = "us", BackgroundWorkerEx worker = null)
        {
            CascConfig config = CascConfig.LoadOnlineStorageConfig(product, region);

            return Open(config, worker);
        }

        private static CascHandler Open(CascConfig config, BackgroundWorkerEx worker)
        {
            using (var _ = new PerfCounter("new CASCHandler()"))
            {
                return new CascHandler(config, worker);
            }
        }

        public override bool FileExists(int fileDataId)
        {
            if (Root is WowRootHandler wrh)
                return wrh.FileExist(fileDataId);
            if (Root is WowTVFSRootHandler wtrh)
                return wtrh.FileExist(fileDataId);
            return false;
        }

        public override bool FileExists(string file) => FileExists(Hasher.ComputeHash(file));

        public override bool FileExists(ulong hash) => RootHandler.GetAllEntries(hash).Any();

        public long GetFileSize(ulong hash)
        {
            if (Root is TVFSRootHandler vfs)
            {
                var vfsEntries = vfs.GetVfsRootEntries(hash);
                if (vfsEntries != null)
                {
                    if (vfsEntries.Count == 1)
                        return vfsEntries[0].ContentLength;
                    else
                        return vfsEntries.Sum(e => (long)e.ContentLength);
                }
            }

            if (GetCKeyForHash(hash, out MD5Hash cKey))
                if (EncodingHandler.GetEntry(cKey, out EncodingEntry enc))
                    return enc.Size;

            return 0;
        }

        private bool GetCKeyForHash(ulong hash, out MD5Hash cKey)
        {
            var rootInfos = RootHandler.GetEntries(hash);
            if (rootInfos.Any())
            {
                cKey = rootInfos.First().cKey;
                return true;
            }

            if ((CascConfig.LoadFlags & LoadFlags.Install) != 0)
            {
                var localeString = RootHandler.Locale.ToString();
                var installInfos = Install.GetEntries().Where(e => e.Hash == hash && e.Tags.Any(t => t.Type == 1 && t.Name == localeString));
                if (installInfos.Any())
                {
                    cKey = installInfos.First().MD5;
                    return true;
                }

                installInfos = Install.GetEntries().Where(e => e.Hash == hash);
                if (installInfos.Any())
                {
                    cKey = installInfos.First().MD5;
                    return true;
                }
            }

            cKey = default;
            return false;
        }

        public bool GetEncodingKey(ulong hash, out MD5Hash eKey)
        {
            if (GetCKeyForHash(hash, out MD5Hash cKey))
                return EncodingHandler.TryGetBestEKey(cKey, out eKey);

            eKey = default;
            return false;
        }

        public override Stream OpenFile(int fileDataId)
        {
            if (Root is WowRootHandler rh)
                return OpenFile(rh.GetHashByFileDataId(fileDataId));

            if (Root is WowTVFSRootHandler rh2)
                return OpenFile(rh2.GetHashByFileDataId(fileDataId));

            throw new NotSupportedException("Opening files by FileDataId only supported for WoW");
        }

        public override Stream OpenFile(string name) => OpenFile(Hasher.ComputeHash(name));

        public override Stream OpenFile(ulong hash)
        {
            if (Root is TVFSRootHandler vfs)
            {
                var vfsEntries = vfs.GetVfsRootEntries(hash);

                if (vfsEntries != null)
                {
                    if (vfsEntries.Count == 1)
                        return OpenFile(vfsEntries[0].eKey);
                    else
                        throw new NotSupportedException();
                }
            }

            if (GetEncodingKey(hash, out MD5Hash eKey))
                return OpenFile(eKey);

            if (CascConfig.ThrowOnFileNotFound)
                throw new FileNotFoundException($"{hash:X16}");

            return null;
        }

        public override void SaveFileTo(ulong hash, string extractPath, string fullName)
        {
            if (Root is TVFSRootHandler vfs)
            {
                var vfsEntries = vfs.GetVfsRootEntries(hash);

                if (vfsEntries != null)
                {
                    if (vfsEntries.Count == 1)
                        SaveFileTo(vfsEntries[0].eKey, extractPath, fullName);
                    else
                        SaveLargeFile(vfsEntries, extractPath, fullName);
                    return;
                }
            }

            if (GetEncodingKey(hash, out MD5Hash eKey))
            {
                SaveFileTo(eKey, extractPath, fullName);
                return;
            }

            if (CascConfig.ThrowOnFileNotFound)
                throw new FileNotFoundException($"{hash:X16}");
        }

        private void SaveLargeFile(List<VfsRootEntry> vfsEntries, string extractPath, string name)
        {
            string fullPath = Path.Combine(extractPath, name);
            string dir = Path.GetDirectoryName(fullPath);

            DirectoryInfo dirInfo = new DirectoryInfo(dir);
            if (!dirInfo.Exists)
                dirInfo.Create();

            using (var fileStream = File.Open(fullPath, FileMode.Create))
            {
                foreach (var entry in vfsEntries)
                {
                    MD5Hash tempEKey = entry.eKey;
                    if (FileIndex?.GetFullEKey(entry.eKey, out var fullEKey) == true)
                        tempEKey = fullEKey;
                    using (Stream stream = OpenFile(tempEKey))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
            }
        }

        public void Clear()
        {
            CDNIndex?.Clear();
            CDNIndex = null;

            foreach (var stream in DataStreams)
                stream.Value.Dispose();

            DataStreams.Clear();

            EncodingHandler?.Clear();
            EncodingHandler = null;

            InstallHandler?.Clear();
            InstallHandler = null;

            LocalIndex?.Clear();
            LocalIndex = null;

            RootHandler?.Clear();
            RootHandler = null;

            DownloadHandler?.Clear();
            DownloadHandler = null;
        }
    }
}
