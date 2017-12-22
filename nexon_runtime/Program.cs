using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace nexon_runtime
{
    class Program
    {
        static void Main(string[] args)
        {
            string parentProcessIdArg = args.First();
            int parentProcessId = int.Parse(parentProcessIdArg);
            Process parent;
            try
            {
                while ((parent = Process.GetProcessById(parentProcessId)) != null)
                {
                    parent.WaitForExit();
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}
