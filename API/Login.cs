using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace API
{
    public class Login
    {
        public string id_token, access_token;
        public int user_no, id_token_expires_in, access_token_expires_in;
        public bool is_verified;

        public string code, message, description;

        public static async Task<Login> PostLogin(string email, string password, bool auto_login, string client_id, string scope, string device_id)
        {
            using (HttpClient client = new HttpClient())
            {
                // Hash *must* be in lowercase.
                string passwordHash = BitConverter.ToString(SHA512.Create().ComputeHash(Encoding.UTF8.GetBytes(password))).Replace("-", "").ToLower();
                string APIPayload = JsonConvert.SerializeObject(new
                {
                    id = email,
                    password = passwordHash,
                    auto_login = auto_login,
                    client_id = client_id,
                    scope = scope,
                    device_id = device_id
                });

                using (HttpResponseMessage APIResponseMessage = await client.PostAsync("https://accounts.nexon.net/account/login/launcher", new StringContent(APIPayload, Encoding.UTF8, "application/json")))
                {
                    string APIResponse = await APIResponseMessage.Content.ReadAsStringAsync();
                    int statusCode = (int)APIResponseMessage.StatusCode;
                    if (statusCode > 500) throw new InvalidOperationException("Invalid response", new Exception(APIResponse));
                    if (statusCode > 400) throw new InvalidOperationException("Invalid data presented", new Exception(APIResponse));

                    return JsonConvert.DeserializeObject<Login>(APIResponse);
                }
            }
        }
    }
}
