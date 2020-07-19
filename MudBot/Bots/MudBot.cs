using System;
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
    public class MudBot : ActivityHandler
    {
        private readonly TcpClientsService _tcpClientsService;

        // Dependency injected dictionary for storing ConversationReference objects used in NotifyController to proactively message users
        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;

        public MudBot(TcpClientsService tcpClientsService, ConcurrentDictionary<string, ConversationReference> conversationReferences)
        {
            _tcpClientsService = tcpClientsService;
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
                _tcpClientsService.ClearTcpClient(userId);

            await _tcpClientsService.SendMessage(userId,
                turnContext.Activity.Text);
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

        protected override Task OnConversationUpdateActivityAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            AddConversationReference(turnContext.Activity as Activity);

            return base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
        }
    }
}
