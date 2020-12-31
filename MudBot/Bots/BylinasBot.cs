using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using MudBot.Services;

namespace MudBot.Bots
{
    public class BylinasBot : ActivityHandler
    {
        private readonly BylinasService _bylinasService;

        public BylinasBot(BylinasService bylinasService)
        {
            _bylinasService = bylinasService;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.GetConversationReference().User.Id;
            var message = turnContext.Activity.Text;

            switch (message)
            {
                case "/start":
                    _bylinasService.CloseTcpClient(userId);
                    break;
                case "/stop":
                {
                    _bylinasService.CloseTcpClient(userId);
                    var reply = MessageFactory.Text("Вы вышли из игры.");

                    reply.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>()
                        {
                            new CardAction() { Title = "Играть!", Type = ActionTypes.ImBack, Value = "Играть!" },
                        },
                    };
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                    return;
                }
                case "/return":
                    message = Environment.NewLine;
                    break;
                case "/help":
                {
                    var reply = MessageFactory.Text(
                        @"Бот позволяет подключиться к текстовой онлайн-игре ""Былины"" (bylins.su). Сайт бота https://github.com/kcherenkov/BylinasBot");
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                    return;
                }
            }

            await _bylinasService.SendMessage(userId, message, turnContext.Activity.GetConversationReference());
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded,
            ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText =
                "МАД(MUD) Былины - это русская и абсолютно бесплатная сетевая текстовая игра.\n\nНаш мир выгодно отличается от остальных игр подобного рода тем, что основан на русских сказках и преданиях, а не является очередной вариацией на тему Средиземья с уже слегка поднадоевшими эльфами, хоббитами и совершенно однотипным набором основных игровых зон. Игра не требует какого-то четкого отыгрывания роли, хотя и старается быть ролевой.";

            var reply = MessageFactory.Text(welcomeText);

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction() { Title = "Играть!", Type = ActionTypes.ImBack, Value = "Играть!" },
                },
            };

            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                }
            }
        }

        public static async Task BotCallback(string message, ITurnContext turnContext,
            CancellationToken cancellationToken)
        {
            //message = message.Replace("яя", "я");
            message = new Regex(@"\x1B\[[^@-~]*[@-~]").Replace(message, String.Empty);
            message = string.Format("```{1}{0}{1}```", message, Environment.NewLine);

            List<string> actions;
            if (message.Contains("1)") && message.Contains("2)"))
            {
                actions = new List<string> {"1", "2"};
                int i = 3;
                while (message.Contains(i + ")"))
                {
                    actions.Add(i.ToString());
                    i++;
                }
            }
            else
            {
                actions = message.Split(' ', '\n')
                    .Where(x => x.Contains('[') && x.Any(char.IsLetter))
                    .Select(x => x.Replace("[", string.Empty).Replace("]", string.Empty))
                    .ToList();
            }

            var exitsPattern = "Вых:";
            var exitsIndex = message.LastIndexOf(exitsPattern);
            if (exitsIndex >= 0)
            {
                exitsIndex += exitsPattern.Length;
                if (message.IndexOf('С', exitsIndex) != -1) actions.Add("С");
                if (message.IndexOf('В', exitsIndex) != -1) actions.Add("В");
                if (message.IndexOf('Ю', exitsIndex) != -1) actions.Add("Ю");
                if (message.IndexOf('З', exitsIndex) != -1) actions.Add("З");
                if (message.IndexOf('^', exitsIndex) != -1) actions.Add("вв");
                if (message.IndexOf('v', exitsIndex) != -1) actions.Add("вн");
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
                    Actions = actions.Select(x => new CardAction {Title = x, Type = ActionTypes.ImBack, Value = x})
                        .ToList()
                };
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
            else
            {
                var reply = MessageFactory.Text(message);
                reply.SuggestedActions = new SuggestedActions();
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }
    }
}
