using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace API
{
    public class AuthGameTicket
    {
        public string Code;

        /// <param name="deviceId">*Must* match the devicde_id used to generate the auth info</param>
        public static async Task<AuthGameTicket> AuthGame(Login authInfo, string productId, string deviceId = "NXLauncher")
        {
            using (HttpClient client = new HttpClient())
            {
                string[] args =
                {
                    $"product_id={productId}",
                    $"id_token={authInfo.id_token}",
                    $"device_id={deviceId}"
                };
                using (HttpResponseMessage APIResponseMessage = await client.PostAsync($"https://api.nexon.io/auth/authorize_game?{string.Join("&", args)}", new StringContent("")))
                {
                    string APIResponse = await APIResponseMessage.Content.ReadAsStringAsync();
                    int statusCode = (int)APIResponseMessage.StatusCode;
                    if (statusCode > 500) throw new InvalidOperationException("Invalid response", new Exception(APIResponse));
                    if (statusCode > 400) throw new UnauthorizedAccessException("Invalid data presented", new Exception(APIResponse));

                    return JsonConvert.DeserializeObject<AuthGameTicket>(APIResponse);
                }
            }
        }
    }
}
