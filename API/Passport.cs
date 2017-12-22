using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace API
{
    public class Passport
    {
        public string user_no, passport, auth_token;

        public async static Task<Passport> GetPassport(Login loginAuth)
        {
            using (HttpClient client = new HttpClient())
            {
                string accessTokenBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(loginAuth.access_token));
                client.DefaultRequestHeaders.Add("Authorization", $"bearer {accessTokenBase64}");

                using (HttpResponseMessage APIResponseMessage = await client.GetAsync("https://api.nexon.io/users/me/passport"))
                {
                    string APIResponse = await APIResponseMessage.Content.ReadAsStringAsync();
                    int statusCode = (int)APIResponseMessage.StatusCode;
                    if (statusCode > 500) throw new InvalidOperationException("Invalid response", new Exception(APIResponse));
                    if (statusCode > 400) throw new InvalidOperationException("Invalid data presented", new Exception(APIResponse));

                    return JsonConvert.DeserializeObject<Passport>(APIResponse);
                }
            }
        }
    }
}
