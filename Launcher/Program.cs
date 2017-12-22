using API;
using Microsoft.AspNetCore.Hosting;
using NXLDownloader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Launcher
{
    public class Program
    {
        static IWebHost IPCHost;
        static void Main(string[] args)
        {
            Config.Instance.Save();
            StartNexonProcess();
            StartIPC();
            // This only needs to be done with doing game management
            //NXLDownloader.Program.Initialize(false);

            DoWhat();
            Config.Instance.Save();
        }

        private static void StartNexonProcess()
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                string exe = Path.Combine(@"C:\Users\inume\Documents\GitHub\Launcher\Launcher\bin\Debug\netcoreapp2.0", "nexon_runtime.exe");
                int currentProcessId = currentProcess.Id;
                Process.Start(exe, currentProcessId.ToString());
            }
        }

        private static void StartIPC()
        {
            NXLIPC.Startup.AuthProvider = new AuthProvider();

            EventWaitHandle wait = new EventWaitHandle(false, EventResetMode.ManualReset);
            Thread ipc = new Thread(() =>
            {
                NXLIPC.Program.Start(wait);
            });

            ipc.Start();

            wait.WaitOne();
            IPCHost = NXLIPC.Program.Host;
        }

        private static void DoWhat()
        {
            int option = 0;
            string resp = null;
            do
            {
                Console.WriteLine("[0] Launch game");
                Console.WriteLine("1 Login");
                //Console.WriteLine("2 Add game");
                //Console.WriteLine("3 Verify game");
                //Console.WriteLine("4 Download game");
                //Console.WriteLine("What would you like to do?");
                resp = Console.ReadLine();
            } while (!string.IsNullOrEmpty(resp) && !int.TryParse(resp.Trim(), out option));

            switch (option)
            {
                case 0:
                    LaunchGames();
                    break;
                case 1:
                    GetCredentials();
                    break;
            }
        }

        private static void GetCredentials()
        {
            Console.WriteLine("We will authenticate with Nexon's servers on your behalf and only store info Nexon gives.");
            Console.WriteLine("Your credentials will not be stored and if Nexon rejects what they previously gave us or it expires, we will need your credentials again.");

            Login authInfo = null;
            do
            {
                Console.Write("Email:");
                string email = Console.ReadLine();
                Console.Write("Password:");
                ConsoleKeyInfo keyPressed;
                string password = "";
                while ((keyPressed = Console.ReadKey(true)).Key != ConsoleKey.Enter) password += keyPressed.KeyChar;
                Console.WriteLine();

                try
                {
                    authInfo = Login.PostLogin(email, password, true, "7853644408", "us.launcher.all", "NXLauncher").Result;
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid credentials, or something went wrong. Try again or try different credentials");
                }
            } while (authInfo == null);

            Config.Instance.AuthInfo = authInfo;
            Config.Instance.Save();
            DoWhat();
        }

        private static void LaunchGames()
        {
            if (Config.Instance.AuthInfo == null)
                Console.WriteLine("Warning: You haven't logged in yet, you probably won't be able to play any games yet.");

            KeyValuePair<string, string>[] games = Config.Instance.GamePaths.ToArray();
            string resp;
            int selectedGame = 0;
            do
            {
                for (int i = 0; i < games.Length; ++i) {
                    Product game = NXLDownloader.Program.GetProduct(games[i].Key);
                    Console.WriteLine($"{i} [{games[i].Key}] - {game?.FriendlyProductName ?? "Unknown Game"}");
                }
                resp = Console.ReadLine();
            } while (!string.IsNullOrEmpty(resp) && int.TryParse(resp, out selectedGame) && selectedGame >= 0 && selectedGame < games.Length);

            string productId = games[selectedGame].Key;
            string gamePath = games[selectedGame].Value;

            Product gameInfo = NXLDownloader.Program.GetProduct(productId);
            LauncherConfig launchConfig = gameInfo.Details.LaunchConfig;
            string exePath = Path.Combine(gamePath, launchConfig.EXEPath);
            string args = string.Join(" ", ReplaceArgs(launchConfig.args, productId));

            ProcessStartInfo startInfo = new ProcessStartInfo(exePath, args);
            startInfo.Verb = "runas";
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = gamePath;
            Process.Start(startInfo);
        }

        public static string[] ReplaceArgs(string[] args, string productId)
        {
            for(int i = 0; i < args.Length; ++i)
            {
                if (args[i].Equals("${passport}"))
                {
                    Passport pass = Passport.GetPassport(Config.Instance.AuthInfo).Result;
                    args[i] = pass.passport;
                }
                if (args[i].Equals("${windir}"))
                    args[i] = $"\"{Environment.GetEnvironmentVariable("WINDIR")}\"";
                if (args[i].Equals("${product_id}")) args[i] = productId;
            }

            return args;
        }
    }
}
