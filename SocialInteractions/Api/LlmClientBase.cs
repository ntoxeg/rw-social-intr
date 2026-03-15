using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SocialInteractions.Api
{
    public abstract class LlmClientBase : ILlmClient
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();

        protected string ApiUrl { get; }
        protected LlmClientConfig Config { get; }
        protected HttpClient HttpClient => SharedHttpClient;

        private bool _disposed;

        protected LlmClientBase(string apiUrl, LlmClientConfig config = null)
        {
            ApiUrl = apiUrl ?? string.Empty;
            Config = config ?? new LlmClientConfig();
        }

        public abstract string Name { get; }

        public async Task<string> GenerateText(string prompt, int? maxLength = null, float? temperature = null, List<string> stopSequence = null, bool? enableXtcSampling = null, int? topK = null, float? topP = null, float? minP = null, float? repetitionPenalty = null)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            try
            {
                object requestBody = BuildRequestBody(prompt, maxLength, temperature, stopSequence, enableXtcSampling, topK, topP, minP, repetitionPenalty);
                string requestJson = PrepareRequestJson(SerializeToJson(requestBody));
                string requestUrl = BuildRequestUrl();

                using (var content = new StringContent(requestJson, Encoding.UTF8, "application/json"))
                using (var request = CreateRequestMessage(requestUrl, content))
                using (var response = await HttpClient.SendAsync(request))
                {
                    string responseBody = await response.Content.ReadAsStringAsync();

                    SLog.Message(string.Format("[SocialInteractions] {0} API Response Status: {1}", Name, response.StatusCode));

                    if (!response.IsSuccessStatusCode)
                    {
                        SLog.Warning(string.Format("[SocialInteractions] {0} API Error (Status {1}): {2}", Name, response.StatusCode, responseBody));
                        return null;
                    }

                    SLog.Message(string.Format("[SocialInteractions] {0} API Response Body: {1}", Name, responseBody));
                    return ExtractText(responseBody);
                }
            }
            catch (HttpRequestException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] {0}: HTTP request failed: {1}", GetType().Name, ex.Message));
                return null;
            }
            catch (TaskCanceledException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] {0}: request timed out or was canceled: {1}", GetType().Name, ex.Message));
                return null;
            }
            catch (SerializationException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] {0}: JSON serialization/deserialization failed: {1}", GetType().Name, ex.Message));
                return null;
            }
            catch (InvalidDataContractException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] {0}: invalid JSON contract: {1}", GetType().Name, ex.Message));
                return null;
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] {0}: Unexpected error during text generation: {1}", GetType().Name, ex.Message));
                return null;
            }
        }

        protected virtual string PrepareRequestJson(string requestJson)
        {
            return requestJson;
        }

        protected virtual HttpRequestMessage CreateRequestMessage(string requestUrl, HttpContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = content;
            request.Headers.TryAddWithoutValidation("User-Agent", "SocialInteractionsMod/1.0");
            return request;
        }

        protected virtual string BuildRequestUrl()
        {
            return ApiUrl;
        }

        protected abstract object BuildRequestBody(string prompt, int? maxLength, float? temperature, List<string> stopSequence, bool? enableXtcSampling, int? topK, float? topP, float? minP, float? repetitionPenalty);

        protected abstract string ExtractText(string responseBody);

        protected static string SerializeToJson(object payload)
        {
            var serializer = new DataContractJsonSerializer(payload.GetType());
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, payload);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        protected static T DeserializeJson<T>(string json)
        {
            var deserializer = new DataContractJsonSerializer(typeof(T));
            using (var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? string.Empty)))
            {
                return (T)deserializer.ReadObject(responseStream);
            }
        }

        protected List<string> BuildStopSequenceList(List<string> stopSequence)
        {
            return stopSequence ?? new List<string>(Config.DefaultStopSequences);
        }

        protected static bool IsValidHeaderValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (char c in value)
            {
                if (char.IsControl(c))
                {
                    return false;
                }
            }

            return true;
        }

        protected static string CleanChatResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return response;
            }

            string cleaned = Regex.Replace(response, @"<thinking>.*?</thinking>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\[thinking\].*?\[/thinking\]", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            return cleaned.Trim();
        }

        protected virtual void Dispose(bool disposing)
        {
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
