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

namespace PS3_PKG_RAP_Sorter
{

    public abstract class Game
    {
        protected int id_;
        protected Dictionary<int, List<Game>> dictionary_;
        private string location_;
        public Game(string location, ref Dictionary<int, List<Game>> dic)
        {
            this.id_ = -1;
            this.location_ = location;
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


        public string Name => Path.GetFileName(Location);

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
                        break;
                    }
                }
            }

            id_ = int.Parse(sb.ToString());
            addToDictionary();

        }

        public override void CopyTo(string path)
        {
           t = Task.Run(() => { File.Copy(Location, path + "\\" + Name); });
        }

        public override void WaitForCopy()
        {
            if (t != null)
                t.Wait();
        }
    }

    public class GameFolder : Game
    {
        private Process proc;
        public GameFolder(string location,ref Dictionary<int, List<Game>> dic) : base(location, ref dic)
        {

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
                        break;
                    }
                }
            }

            id_ = int.Parse(sb.ToString());
            addToDictionary();

        }

        public override void CopyTo(string path)
        {
            proc = new Process();
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.FileName = Path.Combine(Environment.SystemDirectory, "xcopy.exe");
            proc.StartInfo.Arguments = $"\"{Location}\" \"{path + "\\"+ Name}\" /E /I /Y";
            proc.Start();
            
        }

        public override void WaitForCopy()
        {
            if (proc != null)
                proc.WaitForExit();
        }

        public void EnsureFolderStructureIsCorrect(ref int containsPatch,ref int containsGame)
        {
            if (Regex.Match(Name.ToUpper(), "NP").Success)
            {
                containsGame++;
            }
            else if (Regex.Match(Name.ToUpper(), "B[L|C]").Success)
            {
                containsPatch++;
            }
        }
    }


    public class Sorter
    {
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

            //foreach (KeyValuePair<int,List<Game>> v in dic)
            //{
            //    DirectoryInfo di = Directory.CreateDirectory(OUTPUT_LOCATION + "\\" + v.Key);
            //    foreach (Game g in v.Value)
            //    {
            //        g.CopyTo(di.FullName);
            //    }

            //    if (v.Value.Count(x => x is GameFolder) > 0)
            //    {
            //        File.Copy(BACKUP_PKG_MAKER + '\\' + BACKUP_PKG_MAKER_DO, di.FullName + '\\' + BACKUP_PKG_MAKER_DO);
            //        File.Copy(BACKUP_PKG_MAKER + '\\' + BACKUP_PKG_MAKER_PKG1, di.FullName + '\\' + BACKUP_PKG_MAKER_PKG1);
            //        File.Copy(BACKUP_PKG_MAKER + '\\' + BACKUP_PKG_MAKER_PKG2, di.FullName + '\\' + BACKUP_PKG_MAKER_PKG2);
            //    }
            //}


            foreach (KeyValuePair<int, List<Game>> v in dic)
            {
                v.Value.ForEach((Game g) => g.WaitForCopy());
            }


            LinkedList<Process> processes = new LinkedList<Process>();

            foreach (KeyValuePair<int, List<Game>> v in dic)
            {
                int containsPatch = 0;
                int containsGame = 0;
                foreach (Game g in v.Value)
                {
                    GameFolder gf = g as GameFolder;
                    if (gf != null)
                    {
                        gf.EnsureFolderStructureIsCorrect(ref containsPatch, ref containsGame);
                    }
                }

                if (containsPatch == 0)
                {
                    Directory.CreateDirectory(OUTPUT_LOCATION + '\\' + v.Key + '\\' + "BLUS" + v.Key);
                    containsPatch = 1;
                }
                else if (containsGame == 0)
                {
                    Directory.CreateDirectory(OUTPUT_LOCATION + '\\' + v.Key + '\\' + "NPUB" + v.Key);
                    containsGame = 1;
                }

                if (containsGame == 1 && containsPatch == 1)
                {
                    Process proc = new Process();
                    proc.StartInfo.WorkingDirectory = OUTPUT_LOCATION + '\\' + v.Key + '\\';
                    proc.StartInfo.UseShellExecute = true;
                    proc.StartInfo.FileName = "do.bat";
                    proc.StartInfo.Arguments = "< nul";

                    proc.Start();

                    processes.AddLast(proc);
                }
                else
                {
                    Console.WriteLine("You will need to manually review " + v.Key);
                }
            }

            foreach (Process process in processes)
            {
                process.WaitForExit();
            }

            Console.WriteLine("Please correct them and click next to continue");
            Console.ReadLine();

            foreach (KeyValuePair<int, List<Game>> kv in dic)
            {
                string dobat = OUTPUT_LOCATION + '\\' + kv.Key + '\\' + BACKUP_PKG_MAKER_DO;
                if (File.Exists(dobat))
                    File.Delete(dobat);
                string bkpkg1 = OUTPUT_LOCATION + '\\' + kv.Key + '\\' + BACKUP_PKG_MAKER_PKG1;
                if (File.Exists(bkpkg1))
                    File.Delete(bkpkg1);

                string bkpkg2 = OUTPUT_LOCATION + '\\' + kv.Key + '\\' + BACKUP_PKG_MAKER_PKG2;
                if (File.Exists(bkpkg2))
                    File.Delete(bkpkg2);
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
            //SortAndCreatePkgs();

            FormatPkgName();


        }
    }
}
