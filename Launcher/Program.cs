using API;
using Microsoft.AspNetCore.Hosting;
using NXLDownloader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace Launcher
{
    public class Program
    {
        static Dictionary<string, Process> running = new Dictionary<string, Process>();
        static IWebHost IPCHost;
        static void Main(string[] args)
        {
            Config.Instance.Save();
            try {
            StartNexonProcess();
            } catch (Exception ex) {
                Console.WriteLine("[Warn] Couldn't launch NexonRuntime, likely won't be able to launch games");
            }
            StartIPC();
            // This only needs to be done with doing game management
            //NXLDownloader.Program.Initialize(false);

            while (true)
            {
                DoWhat();
                Config.Instance.Save();
            }
        }

        private static void StartNexonProcess()
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                string exe = Path.Combine(@"C:\Users\inume\Documents\Git\Launcher\Launcher\bin\Debug\netcoreapp2.0", "nexon_runtime.exe");
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
                try
                {
                    NXLIPC.Program.Start(wait);
                }
                catch (Exception)
                {

                }
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
                Console.WriteLine("4 Download game");
                //Console.WriteLine("What would you like to do?");
                resp = Console.ReadLine();
            } while (!string.IsNullOrEmpty(resp) && !int.TryParse(resp.Trim(), out option));

            NXLDownloader.Program.AuthInfo = Config.Instance.AuthInfo;
            switch (option)
            {
                case 0:
                    LaunchGames();
                    break;
                case 1:
                    GetCredentials();
                    break;
                case 4:
                    DownloadGame();
                    break;
            }
        }

        private static void DownloadGame()
        {
            NXLDownloader.Program.AuthInfo = Config.Instance.AuthInfo;

            Product p = NXLDownloader.Program.GetProduct("40335");
            string hash = null;
            if (p.Details.Branches == null || !p.Details.Branches.ContainsKey("win32") || p.Details.Branches["win32"].Count == 0) {
                using (HttpClient client = new HttpClient()) {
                    client.DefaultRequestHeaders.Add("Authorization", $"bearer {Convert.ToBase64String(Encoding.UTF8.GetBytes(Config.Instance.AuthInfo.access_token))}");

                    hash = client.GetStringAsync(p.Details.ManifestURL).Result;
                }
            } else hash = p.Details.Branches["win32"].Keys.First();
            NXLDownloader.Program.Download(hash, "40335");
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

            if (running.ContainsKey(productId))
            {
                try
                {
                    running[productId].Kill();
                }
                catch (Exception)
                {

                }
                running.Remove(productId);
            }
            ProcessStartInfo startInfo = new ProcessStartInfo(exePath, args);
            startInfo.Verb = "runas";
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = gamePath;
            Process run = Process.Start(startInfo);
            running.Add(productId, run);
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
                if (args[i].Contains("${language_code}")) args[i] = args[i].Replace("${language_code}", "en_US");
            }

            return args;
        }
    }
}
