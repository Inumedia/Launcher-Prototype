using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Threading;

namespace NXLIPC
{
    public class Program
    {
        public static IWebHost Host;
        public static void Start(EventWaitHandle wait)
        {
            Host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .UseApplicationInsights()
                .UseUrls("http://127.0.0.1:5311")
                .Build();

            wait.Set();

            Host.Run();
        }
    }
}
