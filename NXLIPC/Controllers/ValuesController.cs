using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using NXLIPC.Services;
using System.Collections.Concurrent;
using System.Threading;
using NXLIPC.Requests;
using Newtonsoft.Json;
using API;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace NXLIPC.Controllers
{
    [Route("")]
    public class ValuesController : Controller
    {
        private readonly IAuth _gameAuth;

        public ValuesController(IAuth gameAuth) => _gameAuth = gameAuth;
        static long subscriptions = 0;
        public string LastMessage;

        public static ConcurrentQueue<object> SDKEventMessages = new ConcurrentQueue<object>();

        // GET api/values
        [HttpPost]
        [Route("/subscribe/{type}")]
        public IActionResult Subscribe(string type)
        {
            return Json(new
            {
                status = "success",
                subscriberID = (++subscriptions).ToString()
            });
        }

        [HttpDelete]
        [Route("/messages/{type}/{subscriberId}")]
        public IActionResult DelMessageSub(string type)
        {
            if (type.Equals("sdk-events", StringComparison.CurrentCultureIgnoreCase))
                SDKEventMessages = new ConcurrentQueue<object>();
            return Json(new { status = "success" });
        }

        [HttpGet]
        [Route("/messages/{type}/{subscriberId}")]
        public IActionResult GetResult(string type)
        {
            object[] messages = SDKEventMessages.ToArray();
            if (type.Equals("sdk-events", StringComparison.CurrentCultureIgnoreCase))
                SDKEventMessages = new ConcurrentQueue<object>();

            return Json(new
            {
                messages = messages,
                status = "success"
            });
        }

        [HttpPost]
        [Route("/messages/{type}/{subscriberId}")]
        [Route("/messages/{type}")]
        public IActionResult RecvMessage()
        {
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string message = reader.ReadToEnd();

                if (message.Contains("getProductTicket"))
                {
                    LastMessage = "productTicket";

                    GetProductTicket ticketRequest = JsonConvert.DeserializeObject<GetProductTicket>(message);
                    AuthGameTicket ticket = AuthGameTicket.AuthGame(_gameAuth.GetAuth(), ticketRequest.getProductTicket.productID).Result;

                    SDKEventMessages.Enqueue(new
                    {
                        timestamp = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds,
                        requestID = (object)null,
                        getProductTicketSuccess = new
                        {
                            ticket = ticket.Code,
                            productID = ticketRequest.getProductTicket.productID
                        }
                    });

                    return Json(new { status = "success" });
                }
                else if (message.Contains("getUser")) // Is there the possibility of getting other user profiles?
                {
                    UserProfile self = UserProfile.GetProfile(_gameAuth.GetAuth()).Result;
                    SDKEventMessages.Enqueue(new
                    {
                        timestamp = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds,
                        requestID = (object)null,
                        getUserSuccess = self
                    });

                    return Json(new { status = "success" });
                }
                else if (message.Contains("productActive"))
                {
                    LastMessage = "productActive";
                    return Json(new { satus = true });
                }
                else if (message.Contains("productClosed"))
                {
                    LastMessage = "productClosed";
                    return Json(new { satus = true });
                }

                return Json(new { status = "nope" });
            }
        }
    }
}