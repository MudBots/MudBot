using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MudBot.Bots;

namespace MudBot.Services
{
    public class SphereOfWorldsService
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, TcpClient> _tcpClients = new ConcurrentDictionary<string, TcpClient>();
        public ConcurrentDictionary<string, TcpClient> TcpClients => _tcpClients;

        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly string _appId;
        private static Encoding _encoding = CodePagesEncodingProvider.Instance.GetEncoding("windows-1251");

        public SphereOfWorldsService(IConfiguration configuration, IBotFrameworkHttpAdapter adapter,
            ILogger<SphereOfWorldsService> logger)
        {
            _adapter = adapter;
            _logger = logger;
            _appId = configuration["MicrosoftAppId_SphereOfWorlds"];

            // If the channel is the Emulator, and authentication is not in use,
            // the AppId will be null.  We generate a random AppId for this case only.
            // This is not required for production, since the AppId will have a value.
            if (string.IsNullOrEmpty(_appId))
            {
                _appId = Guid.NewGuid().ToString(); //if no AppId, use a random Guid
            }
        }

        public async Task SendMessage(string conversationId, string message, ConversationReference conversationReference)
        {
            TcpClient tcpClient;
            if (_tcpClients.ContainsKey(conversationId))
            {
                tcpClient = _tcpClients[conversationId];
                if (!tcpClient.Connected)
                {
                    CloseTcpClient(conversationId, tcpClient);
                    return;
                }
                message += Environment.NewLine;
                var bytes = _encoding.GetBytes(message);

                await tcpClient.GetStream().WriteAsync(bytes, 0, bytes.Length);
            }
            else
            {
                tcpClient = new TcpClient("sowmud.ru", 5555);
                _tcpClients[conversationId] = tcpClient;
                _logger.LogInformation("Open new TcpClient for conversationId={0}", conversationId);
                Thread.Sleep(500);
                await ReadData(tcpClient); // get rid of encoding choose
                var chooseEncodingMsg = "1" + Environment.NewLine;
                await tcpClient.GetStream().WriteAsync(_encoding.GetBytes(chooseEncodingMsg), 0,
                    chooseEncodingMsg.Length);
                Task.Run(async () => await ReadDataLoop(conversationId, tcpClient, conversationReference));
            }
        }

        public void CloseTcpClient(string conversationId)
        {
            if (_tcpClients.ContainsKey(conversationId))
            {
                var tcpClient = _tcpClients[conversationId];
                CloseTcpClient(conversationId, tcpClient);
            }
        }

        private async Task ReadDataLoop(string conversationId, TcpClient tcpClient, ConversationReference conversationReference)
        {
            while (true)
            {
                if (!tcpClient.Connected)
                {
                    CloseTcpClient(conversationId, tcpClient);
                    return;
                }

                string message = await ReadData(tcpClient);
                if (string.IsNullOrEmpty(message))
                {
                    tcpClient.Client.Disconnect(false);
                    _logger.LogInformation("Disconnected TcpClient for conversationId={0}", conversationId);
                    continue;
                }

                await ((BotAdapter) _adapter).ContinueConversationAsync(_appId, conversationReference,
                    async (context, token) => await SphereOfWorldsBot.BotCallback(message, context, token),
                    default(CancellationToken));
            }
        }
        
        private void CloseTcpClient(string conversationId, TcpClient tcpClient)
        {
            tcpClient.Close();
            _tcpClients.TryRemove(conversationId, out _);
            _logger.LogInformation("Closed TcpClient for conversationId={0}", conversationId);
        }

        private static async Task<string> ReadData(TcpClient client)
        {
            NetworkStream stream = client.GetStream();

            byte[] myReadBuffer = new byte[1024];

            await using (var ms = new MemoryStream())
            {
                do
                {
                    var numberOfBytesRead = await stream.ReadAsync(myReadBuffer, 0, myReadBuffer.Length);
                    await ms.WriteAsync(myReadBuffer, 0, numberOfBytesRead);
                } while (stream.DataAvailable);

                var resultArray = ms.ToArray();
                int count = resultArray.Length;
                
                // Catch 0xFF 0xF9 "Go ahead" command at the end of stream
                if (count >= 2)
                {
                    if (resultArray[^1] == 249
                        && resultArray[^2] == 255)
                    {
                        count -= 2; // and remove two data bytes
                    }
                }

                return _encoding.GetString(resultArray, 0, count);
            }
        }
    }
}
