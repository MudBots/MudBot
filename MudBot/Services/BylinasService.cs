using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MudBot.Services
{
    public class BylinasService
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, TcpClient> _tcpClients = new Dictionary<string, TcpClient>();
        public Dictionary<string, TcpClient> TcpClients => _tcpClients;

        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly string _appId;
        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;
        private static readonly Encoding _encoding = Encoding.Default;

        public BylinasService(IConfiguration configuration, IBotFrameworkHttpAdapter adapter,
            ConcurrentDictionary<string, ConversationReference> conversationReferences, ILogger<BylinasService> logger)
        {
            _adapter = adapter;
            _conversationReferences = conversationReferences;
            _logger = logger;
            _appId = configuration["MicrosoftAppId"];

            // If the channel is the Emulator, and authentication is not in use,
            // the AppId will be null.  We generate a random AppId for this case only.
            // This is not required for production, since the AppId will have a value.
            if (string.IsNullOrEmpty(_appId))
            {
                _appId = Guid.NewGuid().ToString(); //if no AppId, use a random Guid
            }
        }

        public async Task SendMessage(string userId, string message)
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
                Task.Run(() => ReadDataLoop(userId, tcpClient, _conversationReferences[userId]));
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
                    async (context, token) =>
                    {
                        //message = message.Replace("яя", "я");
                        message = new Regex(@"\x1B\[[^@-~]*[@-~]").Replace(message, String.Empty);
                        message = string.Format("```{1}{0}{1}```", message, Environment.NewLine);

                        List<string> actions;
                        if (message.Contains("1)") && message.Contains("2)"))
                        {
                            actions = new List<string>
                            {
                                "1",
                                "2"
                            };
                            int i = 3;
                            while (message.Contains(i + ")"))
                            {
                                actions.Add(i.ToString());
                                i++;
                            }
                        }
                        else
                        {
                            actions = message.Split(' ', '\n').Where(x =>
                                    x.Contains('[') && x.Any(char.IsLetter))
                                .Select(x => x.Replace("[", string.Empty).Replace("]", string.Empty)
                                ).ToList();
                        }

                        var exitsPattern = "Вых:";
                        var exitsIndex = message.LastIndexOf(exitsPattern);
                        if (exitsIndex >= 0)
                        {
                            exitsIndex += exitsPattern.Length;
                            if (message.IndexOf('С', exitsIndex) != -1)
                                actions.Add("С");
                            if (message.IndexOf('В', exitsIndex) != -1)
                                actions.Add("В");
                            if (message.IndexOf('Ю', exitsIndex) != -1)
                                actions.Add("Ю");
                            if (message.IndexOf('З', exitsIndex) != -1)
                                actions.Add("З");
                            if (message.IndexOf('^', exitsIndex) != -1)
                                actions.Add("вв");
                            if (message.IndexOf('v', exitsIndex) != -1)
                                actions.Add("вн");
                        }

                        if (message.Contains("<RETURN>"))
                        {
                            actions.Add("/return");
                        }

                        if (actions.Count > 0)
                        {
                            var reply = MessageFactory.Text(message);
                            reply.SuggestedActions = new SuggestedActions()
                            {
                                Actions = actions.Select(x => new CardAction
                                {
                                    Title = x,
                                    Type = ActionTypes.ImBack,
                                    Value = x
                                }).ToList()
                            };
                            await context.SendActivityAsync(reply, token);
                        }
                        else
                        {
                            var reply = MessageFactory.Text(message);
                            reply.SuggestedActions = new SuggestedActions();
                            await context.SendActivityAsync(reply, token);
                        }
                    }, default(CancellationToken));
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
