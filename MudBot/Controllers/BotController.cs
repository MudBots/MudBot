using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using MudBot.Bots;

namespace MudBot.Controllers
{
    // This ASP Controller is created to handle a request. Dependency Injection will provide the Adapter and IBot
    // implementation at runtime. Multiple different IBot implementations running at different endpoints can be
    // achieved by specifying a more specific type for the bot constructor argument.
    [Route("api/messages")]
    [ApiController]
    public class BotController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly BylinasBot _bylinasBot;
        private readonly SphereOfWorldsBot _sphereOfWorldsBot;

        public BotController(IBotFrameworkHttpAdapter adapter, BylinasBot bylinasBot, SphereOfWorldsBot sphereOfWorldsBot)
        {
            _adapter = adapter;
            _bylinasBot = bylinasBot;
            _sphereOfWorldsBot = sphereOfWorldsBot;
        }

        [HttpPost("bylinas")]
        public async Task PostBylinasAsync()
        {
            // Delegate the processing of the HTTP POST to the adapter.
            // The adapter will invoke the bot.
            await _adapter.ProcessAsync(Request, Response, _bylinasBot);
        }
        
        [HttpPost("sow")]
        public async Task PostSphereOfWorldsAsync()
        {
            // Delegate the processing of the HTTP POST to the adapter.
            // The adapter will invoke the bot.
            await _adapter.ProcessAsync(Request, Response, _sphereOfWorldsBot);
        }
    }
}
