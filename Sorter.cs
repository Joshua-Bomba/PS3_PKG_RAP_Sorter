using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Security;
using System.Web.Services.Discovery;

namespace PS3_PKG_RAP_Sorter
{

    public abstract class Game
    {
        protected int id_;
        protected Dictionary<int, List<Game>> dictionary_;
        protected int cutOffIndex_;
        private string location_;
        public Game(string location, ref Dictionary<int, List<Game>> dic)
        {
            this.id_ = -1;
            this.location_ = location;
            this.cutOffIndex_ = -1;
            dictionary_ = dic;
        }

        public string Location => location_;

        public void addToDictionary()
        {
            if (!dictionary_.ContainsKey(id_))
            {
                dictionary_[id_] = new List<Game>();
            }

            dictionary_[id_].Add(this);
        }


        public virtual string Name => Path.GetFileName(Location);

        public string CopyFolder { get; set; }

        public abstract void FindId();
        public abstract void CopyTo(string path);

        public abstract void WaitForCopy();
    }

    public class GameRap : Game
    {
        private Task t;
        public GameRap(string location, ref Dictionary<int, List<Game>> dic) : base(location, ref dic)
        {

        }

        public override string Name => Path.GetFileNameWithoutExtension(Location);

        public override void FindId()
        {    
            StringBuilder sb = new StringBuilder();
            bool numberRead = false;
            for (int i = Name.IndexOf('-'); i < Name.Length; i++)
            {
                if (Char.IsNumber(Name[i]))
                {
                    if (!numberRead)
                        numberRead = true;
                    sb.Append(Name[i]);
                }
                else
                {
                    if (numberRead)
                    {
                        cutOffIndex_ = i;
                        break;
                    }
                }
            }

            id_ = int.Parse(sb.ToString());
            addToDictionary();

        }

        public override void CopyTo(string path)
        {
           //extremely unessarly terrible
           t = Task.Run(() =>
           {
               if(!File.Exists(path)&&!Sorter.DRY_RUN)
                File.Copy(Location, path);
           });
        }

        public override void WaitForCopy()
        {
            t?.Wait();
        }
    }

    public enum GameType
    {
        None,
        Game,
        Patch,
    }

    public class GameFolder : Game
    {
        private Process copyProc;
        private Process pkgProc;
        private GameType gt;
        public GameFolder(string location,ref Dictionary<int, List<Game>> dic) : base(location, ref dic)
        {
            copyProc = null;
            pkgProc = null;
            gt = GameType.None;
        }

        public override void FindId()
        {
            StringBuilder sb = new StringBuilder();

            bool numberRead = false;
            for (int i = 0; i < Name.Length; i++)
            {
                if (Char.IsNumber(Name[i]))
                {
                    if(!numberRead)
                        numberRead = true;
                    sb.Append(Name[i]);
                }
                else
                {
                    if (numberRead)
                    {
                        cutOffIndex_ = i;
                        break;
                    }
                }
            }

            id_ = int.Parse(sb.ToString());
            addToDictionary();

        }

        public override void CopyTo(string path)
        {
            CopyFolder = path;
            if(!Sorter.DRY_RUN)
                Directory.CreateDirectory(CopyFolder);

            if(!File.Exists(CopyFolder + '\\' + Sorter.BACKUP_PKG_MAKER_DO)&& !Sorter.DRY_RUN)
                File.Copy(Sorter.BACKUP_PKG_MAKER + '\\' + Sorter.BACKUP_PKG_MAKER_DO, CopyFolder + '\\' + Sorter.BACKUP_PKG_MAKER_DO);
            if (!File.Exists(CopyFolder + '\\' + Sorter.BACKUP_PKG_MAKER_PKG1)&&!Sorter.DRY_RUN)
                File.Copy(Sorter.BACKUP_PKG_MAKER + '\\' + Sorter.BACKUP_PKG_MAKER_PKG1, CopyFolder + '\\' + Sorter.BACKUP_PKG_MAKER_PKG1);
            if (!File.Exists(CopyFolder + '\\' + Sorter.BACKUP_PKG_MAKER_PKG2)&&!Sorter.DRY_RUN)
                File.Copy(Sorter.BACKUP_PKG_MAKER + '\\' + Sorter.BACKUP_PKG_MAKER_PKG2, CopyFolder + '\\' + Sorter.BACKUP_PKG_MAKER_PKG2);

            if (!Sorter.DRY_RUN)
            {
                copyProc = new Process();
                copyProc.StartInfo.UseShellExecute = true;
                copyProc.StartInfo.FileName = Path.Combine(Environment.SystemDirectory, "xcopy.exe");
                copyProc.StartInfo.Arguments = $"\"{Location}\" \"{CopyFolder + "\\" + Name}\" /E /I /Y";
                copyProc.Start();
            }


        }

        public override void WaitForCopy()
        {
            copyProc?.WaitForExit();
        }

        public void WaitForPkgGen()
        {
            pkgProc?.WaitForExit();
        }

        public void EnsureFolderStructureIsCorrect()
        {
            string location = "";
            if (Regex.Match(Name.ToUpper(), "NP").Success)
            {
                location = CopyFolder + '\\' + "BLUS" + id_;
                gt = GameType.Game;
            }
            else if (Regex.Match(Name.ToUpper(), "B[L|C]").Success)
            {
                location = CopyFolder + '\\' + "NPUB" + id_;
                gt = GameType.Patch;
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                if(cutOffIndex_ != -1)
                    location += Name.Substring(cutOffIndex_);
                if(!Sorter.DRY_RUN)
                    Directory.CreateDirectory(location);
            }
        }

        public void DeletePkgMaker()
        {
            string dobat = CopyFolder +'\\' + Sorter.BACKUP_PKG_MAKER_DO;
            if (File.Exists(dobat)&& !Sorter.DRY_RUN)
                File.Delete(dobat);
            string bkpkg1 = CopyFolder + '\\' + Sorter.BACKUP_PKG_MAKER_PKG1;
            if (File.Exists(bkpkg1)&& !Sorter.DRY_RUN)
                File.Delete(bkpkg1);
            string bkpkg2 = CopyFolder + '\\' + Sorter.BACKUP_PKG_MAKER_PKG2;
            if (File.Exists(bkpkg2)&& !Sorter.DRY_RUN)
                File.Delete(bkpkg2);
        }

        public void CreatePackages()
        {
            if (!Sorter.DRY_RUN)
            {
                pkgProc = new Process();
                pkgProc.StartInfo.WorkingDirectory = CopyFolder;
                pkgProc.StartInfo.UseShellExecute = true;
                pkgProc.StartInfo.FileName = "do.bat";
                pkgProc.StartInfo.Arguments = "< nul";

                pkgProc.Start();
            }
        }

        public void FormatPackages(IList<GameRap> raps)
        {
            string[] pkgs = Directory.GetFiles(CopyFolder, "*.pkg");

            if (pkgs.Length == 2)
            {
                string pkgFile = "";
                switch (gt)
                {
                    case GameType.Game :
                        if(Directory.Exists(CopyFolder + '\\' + "BLUS" + id_)&& !Sorter.DRY_RUN)
                            Directory.Delete(CopyFolder + '\\' + "BLUS" + id_);
                        pkgFile = pkgs.FirstOrDefault(x => x.Contains("PATCH"));
                        if(pkgFile != null&&File.Exists(pkgFile)&& !Sorter.DRY_RUN)
                            File.Delete(pkgFile);
                        pkgFile = pkgs.FirstOrDefault(x => x.Contains("GAME"));
                        GameRap rap = null;
                        if (raps.Count > 1)
                        {
                            while (rap == null)
                            {
                                Log.Instance.LogLine("Please Select Correct rap");
                                for (int i = 0; i < raps.Count; i++)
                                {
                                    Log.Instance.LogLine(i + ":" + raps[i].Name);
                                }

                                if(!int.TryParse(Console.ReadLine(), out int result))
                                    continue;

                                if (result >= 0 && result < raps.Count)
                                {
                                    rap = raps[result];
                                }

                            }

                        }
                        else if(raps.Count == 1)
                        {
                            rap = raps.First();
                            
                        }

                        if (rap != null&&pkgFile != null&& !Sorter.DRY_RUN)
                        {
                            File.Move(pkgFile,CopyFolder + "\\" + rap.Name + ".pkg");
                        }
                        else if (rap == null)
                        {
                            Log.Instance.LogLine("No Rap File Included for " + CopyFolder);
                        }

                        break;
                    case GameType.Patch:
                        if(Directory.Exists(CopyFolder + '\\' + "NPUB" + id_)&& !Sorter.DRY_RUN)
                            Directory.Delete(CopyFolder + '\\' + "NPUB" + id_);
                        pkgFile = pkgs.FirstOrDefault(x => x.Contains("GAME"));
                        if (pkgFile != null && File.Exists(pkgFile)&& !Sorter.DRY_RUN)
                            File.Delete(pkgFile);
                        break;
                    case GameType.None:
                        break;
                }
            }
            else
            {
                Log.Instance.LogLine("There was an issue generating pkgs for " +CopyFolder);
            }

            
        }

        public void OutputPkg()
        {
            string[] pkgs = Directory.GetFiles(CopyFolder, "*.pkg");
            if (pkgs.Length == 1&& !Sorter.DRY_RUN)
            {
                string path = Sorter.OUTPUT_LOCATION + "\\" + Path.GetFileName(pkgs.First());
                File.Move(pkgs.First(),path);
            }
        }
    }


    public class Sorter
    {
        public static bool DRY_RUN = false;

        public const string INPUT_LOCATION = @"D:\uncritical\PS3_CONSOLE\games";
        public const string OUTPUT_LOCATION = @"D:\uncritical\PS3_CONSOLE\han_install_packages\games";
        public const string RAP_LOCATION = @"D:\uncritical\PS3_CONSOLE\han_install_packages\rif converter\created_raps";
        public const string BACKUP_PKG_MAKER = @"D:\uncritical\PS3_CONSOLE\han_install_packages\make-backup-pkg";
        public const string BACKUP_PKG_MAKER_DO = @"\do.bat";
        public const string BACKUP_PKG_MAKER_PKG1 = @"makepkg1.exe";
        public const string BACKUP_PKG_MAKER_PKG2 = @"makepkg2.exe";


        public static void SortAndCreatePkgs()
        {
            Dictionary<int, List<Game>> dic = new Dictionary<int, List<Game>>();
            foreach (string location in Directory.GetDirectories(INPUT_LOCATION))
            {
                Game g = new GameFolder(location, ref dic);
                g.FindId();
            }

            foreach (string location in Directory.GetFiles(RAP_LOCATION))
            {
                Game g = new GameRap(location, ref dic);
                g.FindId();
            }

            foreach (KeyValuePair<int, List<Game>> v in dic)
            {
                DirectoryInfo di = Sorter.DRY_RUN? new DirectoryInfo(OUTPUT_LOCATION + "\\" + v.Key) : Directory.CreateDirectory(OUTPUT_LOCATION + "\\" + v.Key);
                foreach (Game g in v.Value)
                {
                    g.CopyTo(di.FullName + '\\' + g.Name);
                }
            }


            foreach (KeyValuePair<int, List<Game>> v in dic)
            {
                v.Value.ForEach((Game g) => g.WaitForCopy());
            }


            foreach (KeyValuePair<int, List<Game>> v in dic)
            {
                foreach (Game g in v.Value)
                {
                    if (g is GameFolder gf)
                    {
                        gf.EnsureFolderStructureIsCorrect();
                        gf.CreatePackages();

                    }
                }
            }

            foreach (KeyValuePair<int, List<Game>> v in dic)
            {
                foreach (Game g in v.Value)
                {
                    GameFolder gf = g as GameFolder;
                    gf?.WaitForPkgGen();
                    gf?.DeletePkgMaker();
                }
            }

            foreach (KeyValuePair<int, List<Game>> v in dic)
            {

                List<GameRap> raps = new List<GameRap>();
                LinkedList<GameFolder> games = new LinkedList<GameFolder>();
                foreach (Game g in v.Value)
                {
                    if (g is GameFolder gf)
                    {
                        games.AddLast(gf);
                    }
                    else if (g is GameRap gr)
                    {
                        raps.Add(gr);
                    }
                }

                foreach (GameFolder gf in games)
                {
                    gf.FormatPackages(raps);
                    gf.OutputPkg();
                }
            }

        }


        public static void FormatPkgName()
        {
            string[] locations = Directory.GetDirectories(OUTPUT_LOCATION);

            foreach (string s in locations)
            {
                string[] files = Directory.GetFiles(s);
            }
        }


        public static void Main(string[] args)
        {
            using(Log l = new Log())
            {
                Log.Instance = l;
                SortAndCreatePkgs();
            }


            //FormatPkgName();


        }
    }
}
