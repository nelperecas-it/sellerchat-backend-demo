using Microsoft.Extensions.Options;
using SCIABackendDemo.Configuration;

namespace SCIABackendDemo.Services
{
    public class PromptService
    {
        private string _currentPrompt;

        public PromptService(IOptions<UltravoxOptions> opts)
        {
            _currentPrompt = opts.Value.SystemPrompt;
        }

        public string CurrentPrompt => _currentPrompt;
        public void SetPrompt(string prompt) => _currentPrompt = prompt;
    }
}
