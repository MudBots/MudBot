using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;

namespace MudBot
{
    public class MultiCredentialProvider : ICredentialProvider
    {
        private readonly Dictionary<string, string> _credentials = new Dictionary<string, string>();

        public MultiCredentialProvider(IConfiguration configuration)
        {
            if (!string.IsNullOrEmpty(configuration["MicrosoftAppId_Bylinas"]))
                _credentials.Add(configuration["MicrosoftAppId_Bylinas"], configuration["MicrosoftAppPassword_Bylinas"]);
            
            if (!string.IsNullOrEmpty(configuration["MicrosoftAppId_SphereOfWorlds"]))
                _credentials.Add(configuration["MicrosoftAppId_SphereOfWorlds"], configuration["MicrosoftAppPassword_SphereOfWorlds"]);
        }

        public Task<bool> IsValidAppIdAsync(string appId)
        {
            return Task.FromResult(this._credentials.ContainsKey(appId));
        }

        public Task<string> GetAppPasswordAsync(string appId)
        {
            return Task.FromResult(this._credentials.ContainsKey(appId) ? this._credentials[appId] : null);
        }

        public Task<bool> IsAuthenticationDisabledAsync()
        {
            return Task.FromResult(!this._credentials.Any());
        }
    }
}