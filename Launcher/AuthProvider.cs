using API;
using NXLIPC.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Launcher
{
    public class AuthProvider : IAuth
    {
        public Login GetAuth() => Config.Instance.AuthInfo;
    }
}
 