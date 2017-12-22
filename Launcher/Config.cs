using API;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Launcher
{
    public class Config
    {
        static Config _instance;

        public static Config Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (File.Exists("settings.json")) _instance = JsonConvert.DeserializeObject<Config>(File.ReadAllText("settings.json"));
                    else _instance = new Config();
                }
                return _instance;
            }
        }

        public Login AuthInfo;
        public Dictionary<string, string> GamePaths;
        public string LastPlayedGame;

        public void Save() => File.WriteAllText("settings.json", JsonConvert.SerializeObject(this, Formatting.Indented));
    }
}
