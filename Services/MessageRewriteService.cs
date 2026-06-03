using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ToastAlert.Services
{
    public class MessageRewriteService
    {
        private readonly ConcurrentDictionary<IEasterEgg, Dictionary<string, string>> _replacements = new();

        public void RegisterReplacements(IEasterEgg owner, Dictionary<string, string> replacements)
        {
            _replacements[owner] = replacements;
        }

        public void Unregister(IEasterEgg owner)
        {
            _replacements.TryRemove(owner, out _);
        }

        public string Rewrite(string originalMessage)
        {
            if (_replacements.IsEmpty) return originalMessage;
            string result = originalMessage;
            foreach (var dict in _replacements.Values)
            {
                foreach (var pair in dict)
                {
                    result = result.Replace(pair.Key, pair.Value);
                }
            }
            return result;
        }
    }
}