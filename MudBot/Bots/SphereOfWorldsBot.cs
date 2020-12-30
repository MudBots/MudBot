﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using MudBot.Services;

namespace MudBot.Bots
{
    public class SphereOfWorldsBot : ActivityHandler
    {
        private readonly SphereOfWorldsService _sphereOfWorldsService;

        // Dependency injected dictionary for storing ConversationReference objects used in NotifyController to proactively message users
        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;

        public SphereOfWorldsBot(SphereOfWorldsService bylinasService, ConcurrentDictionary<string, ConversationReference> conversationReferences)
        {
            _sphereOfWorldsService = bylinasService;
            _conversationReferences = conversationReferences;
        }

        private void AddConversationReference(Activity activity)
        {
            var conversationReference = activity.GetConversationReference();
            _conversationReferences.AddOrUpdate(conversationReference.User.Id, conversationReference, (key, newValue) => conversationReference);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            AddConversationReference(turnContext.Activity as Activity);
            var userId = turnContext.Activity.GetConversationReference().User.Id;

            if (turnContext.Activity.Text == "/start")
                _sphereOfWorldsService.CloseTcpClient(userId);

            if (turnContext.Activity.Text == "/stop")
            {
                _sphereOfWorldsService.CloseTcpClient(userId);
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

            if (turnContext.Activity.Text == "/return")
            {
                await _sphereOfWorldsService.SendMessage(userId, Environment.NewLine);
                return;
            }

            if (turnContext.Activity.Text == "/help")
            {
                var reply = MessageFactory.Text(
                    @"Бот позволяет подключиться к текстовой онлайн-игре ""Сфера Миров"" (sowmud.ru). Сайт бота https://github.com/kcherenkov/BylinasBot");
                await turnContext.SendActivityAsync(reply, cancellationToken);
                return;
            }

            await _sphereOfWorldsService.SendMessage(userId,
                turnContext.Activity.Text);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded,
            ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText =
                "Перед вами проект многопользовательской сетевой игры типа MultiUser Dimension/Dungeon (MUD), или как его еще называют по русски – МУД или МАД. Это проект принципиально нового, русскоязычного мада, который создается опытными игроками, стремящимися сделать по настоящему интересную и популярную многопользовательскую игру.";

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

        protected override Task OnConversationUpdateActivityAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            AddConversationReference(turnContext.Activity as Activity);

            return base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
        }
    }
}
