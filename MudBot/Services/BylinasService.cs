using System;
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
    public class BylinasService
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, TcpClient> _tcpClients = new Dictionary<string, TcpClient>();
        public Dictionary<string, TcpClient> TcpClients => _tcpClients;

        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly string _appId;
        private static readonly Encoding _encoding = Encoding.Default;

        public BylinasService(IConfiguration configuration, IBotFrameworkHttpAdapter adapter,
            ILogger<BylinasService> logger)
        {
            _adapter = adapter;
            _logger = logger;
            _appId = configuration["MicrosoftAppId_Bylinas"];

            // If the channel is the Emulator, and authentication is not in use,
            // the AppId will be null.  We generate a random AppId for this case only.
            // This is not required for production, since the AppId will have a value.
            if (string.IsNullOrEmpty(_appId))
            {
                _appId = Guid.NewGuid().ToString(); //if no AppId, use a random Guid
            }
        }

        public async Task SendMessage(string userId, string message, ConversationReference conversationReference)
        {
            TcpClient tcpClient;
            if (_tcpClients.ContainsKey(userId))
            {
                tcpClient = _tcpClients[userId];
                if (!tcpClient.Connected)
                {
                    CloseTcpClient(userId, tcpClient);
                    return;
                }
                message += Environment.NewLine;
                var bytes = _encoding.GetBytes(message);

                await tcpClient.GetStream().WriteAsync(bytes, 0, bytes.Length);
            }
            else
            {
                tcpClient = new TcpClient("bylins.su", 4000);
                _tcpClients[userId] = tcpClient;
                _logger.LogInformation("Open new TcpClient for userid={0}", userId);
                Thread.Sleep(500);
                await ReadData(tcpClient); // get rid of encoding choose
                var chooseEncodingMsg = "5" + Environment.NewLine;
                await tcpClient.GetStream().WriteAsync(_encoding.GetBytes(chooseEncodingMsg), 0,
                    chooseEncodingMsg.Length);
                Task.Run(async () => await ReadDataLoop(userId, tcpClient, conversationReference));
            }
        }

        public void CloseTcpClient(string userId)
        {
            if (_tcpClients.ContainsKey(userId))
            {
                var tcpClient = _tcpClients[userId];
                CloseTcpClient(userId, tcpClient);
            }
        }

        private async Task ReadDataLoop(string userId, TcpClient tcpClient, ConversationReference conversationReference)
        {
            while (true)
            {
                if (!tcpClient.Connected)
                {
                    CloseTcpClient(userId, tcpClient);
                    return;
                }

                string message = await ReadData(tcpClient);
                if (string.IsNullOrEmpty(message))
                {
                    tcpClient.Client.Disconnect(false);
                    _logger.LogInformation("Disconnected TcpClient for userId={0}", userId);
                    continue;
                }

                await ((BotAdapter) _adapter).ContinueConversationAsync(_appId, conversationReference,
                    async (context, token) => await BylinasBot.BotCallback(message, context, token),
                    default(CancellationToken));
            }
        }

        private void CloseTcpClient(string userId, TcpClient tcpClient)
        {
            tcpClient.Close();
            _tcpClients.Remove(userId);
            _logger.LogInformation("Closed TcpClient for userId={0}", userId);
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
