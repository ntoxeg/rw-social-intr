using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialInteractions.Api
{
    public interface ILlmClient : IDisposable
    {
        string Name { get; }

        Task<string> GenerateText(
            string prompt,
            int? maxLength = null,
            float? temperature = null,
            List<string> stopSequence = null,
            bool? enableXtcSampling = null,
            int? topK = null,
            float? topP = null,
            float? minP = null,
            float? repetitionPenalty = null);
    }
}
