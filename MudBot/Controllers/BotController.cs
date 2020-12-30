using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using MudBot.Bots;
using MudBot.Services;

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
        private readonly BylinasService _bylinasService;
        private readonly SphereOfWorldsService _sphereOfWorldsService;

        public BotController(IBotFrameworkHttpAdapter adapter, BylinasBot bylinasBot,
            SphereOfWorldsBot sphereOfWorldsBot, BylinasService bylinasService,
            SphereOfWorldsService sphereOfWorldsService)
        {
            _adapter = adapter;
            _bylinasBot = bylinasBot;
            _sphereOfWorldsBot = sphereOfWorldsBot;
            _bylinasService = bylinasService;
            _sphereOfWorldsService = sphereOfWorldsService;
        }

        [HttpGet("statistics")]
        public string GetStatistics()
        {
            string result = $"Bylinas active TcpClients = {_bylinasService.TcpClients.Count}\n";
            foreach (var tcpClient in _bylinasService.TcpClients)
            {
                result += "  " + tcpClient.Key + "\n";
            }
            result += $"\nSphere of Worlds active TcpClients = {_sphereOfWorldsService.TcpClients.Count}\n";
            foreach (var tcpClient in _sphereOfWorldsService.TcpClients)
            {
                result += "  " + tcpClient.Key + "\n";
            }

            return result;
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
