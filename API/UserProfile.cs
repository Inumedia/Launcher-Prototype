using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace API
{
    public class UserProfile
    {
        public string profile_name, relation, presence;
        public int user_no, membership_no, privacy_level, relation_code;
        public AvatarInfo avatar;

        public static async Task<UserProfile> GetProfile(Login loginAuth, string userId = "me")
        {
            using (HttpClient client = new HttpClient())
            {
                string accessTokenBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(loginAuth.access_token));
                client.DefaultRequestHeaders.Add("Authorization", $"bearer {accessTokenBase64}");

                using (HttpResponseMessage APIResponseMessage = await client.GetAsync($"https://api.nexon.io/users/{userId}/profile"))
                {
                    string APIResponse = await APIResponseMessage.Content.ReadAsStringAsync();
                    int statusCode = (int)APIResponseMessage.StatusCode;
                    if (statusCode > 500) throw new InvalidOperationException("Invalid response", new Exception(APIResponse));
                    if (statusCode > 400) throw new InvalidOperationException("Invalid data presented", new Exception(APIResponse));

                    return JsonConvert.DeserializeObject<UserProfile>(APIResponse);
                }
            }
        }
    }

    public class AvatarInfo
    {
        public int avatar_id;
        public string avatar_img;
        public bool is_default, is_custom_avatar;
    }
}
