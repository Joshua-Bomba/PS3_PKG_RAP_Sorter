using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PS3_PKG_RAP_Sorter
{
    public class Log : IDisposable
    {
        private string file;
        private FileStream fs;
        private StreamWriter o;

        public static Log Instance { get; set; }

        public Log()
        {
            file = "log-" + DateTime.Now.ToString();
            fs = new FileStream(file,FileMode.CreateNew);
            o = new StreamWriter(fs);

        }

        public void LogLine(object s)
        {
            Console.WriteLine(s);
            o.WriteLine(s);
        }

        public void Dispose()
        {
            o.Close();
            fs.Close();
        }
    }
}
