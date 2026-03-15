using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using SocialInteractions.Api;
using Xunit;

namespace SocialInteractions.Tests
{
    public class LlmClientConfigTests
    {
        private static Assembly unityCoreModuleShim;
        private static bool resolverRegistered;

        [Fact]
        public void Defaults_AreExpectedValues()
        {
            var config = new LlmClientConfig();

            Assert.Equal(1024, config.MaxTokens);
            Assert.Equal(0.7f, config.Temperature);
            Assert.Equal(40, config.TopK);
            Assert.Equal(1.0f, config.TopP);
            Assert.Equal(0.05f, config.MinP);
            Assert.Equal(1.0f, config.RepetitionPenalty);
            Assert.False(config.EnableXtcSampling);
            Assert.True(config.DisableThinking);
            Assert.True(config.ForceChatCompletion);
            Assert.Equal(string.Empty, config.GeminiModelName);
            Assert.NotNull(config.DefaultStopSequences);
            Assert.Empty(config.DefaultStopSequences);
        }

        [Fact]
        public void FromSettings_Null_ReturnsDefaults()
        {
            EnsureUnityCoreModuleShim();
            var config = LlmClientConfig.FromSettings(null);

            Assert.Equal(1024, config.MaxTokens);
            Assert.Equal(0.7f, config.Temperature);
            Assert.Equal(40, config.TopK);
            Assert.Equal(1.0f, config.TopP);
            Assert.Equal(0.05f, config.MinP);
            Assert.Equal(1.0f, config.RepetitionPenalty);
            Assert.False(config.EnableXtcSampling);
            Assert.True(config.DisableThinking);
            Assert.True(config.ForceChatCompletion);
            Assert.Equal(string.Empty, config.GeminiModelName);
            Assert.NotNull(config.DefaultStopSequences);
            Assert.Empty(config.DefaultStopSequences);
        }

        [Fact]
        public void Properties_CanBeSetIndependently()
        {
            var config = new LlmClientConfig();

            config.MaxTokens = 2048;
            config.Temperature = 0.25f;
            config.TopK = 10;
            config.TopP = 0.9f;
            config.MinP = 0.1f;
            config.RepetitionPenalty = 1.2f;
            config.EnableXtcSampling = true;
            config.DisableThinking = false;
            config.ForceChatCompletion = false;
            config.GeminiModelName = "gemini-test";
            config.DefaultStopSequences.Add("END");

            Assert.Equal(2048, config.MaxTokens);
            Assert.Equal(0.25f, config.Temperature);
            Assert.Equal(10, config.TopK);
            Assert.Equal(0.9f, config.TopP);
            Assert.Equal(0.1f, config.MinP);
            Assert.Equal(1.2f, config.RepetitionPenalty);
            Assert.True(config.EnableXtcSampling);
            Assert.False(config.DisableThinking);
            Assert.False(config.ForceChatCompletion);
            Assert.Equal("gemini-test", config.GeminiModelName);
            Assert.Single(config.DefaultStopSequences);
            Assert.Equal("END", config.DefaultStopSequences[0]);
        }

        private static void EnsureUnityCoreModuleShim()
        {
            if (!resolverRegistered)
            {
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                resolverRegistered = true;
            }

            var loaded = AppDomain.CurrentDomain
                .GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, "UnityEngine.CoreModule", StringComparison.Ordinal));

            if (loaded)
            {
                return;
            }

            unityCoreModuleShim = BuildUnityCoreModuleShim();
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var requested = new AssemblyName(args.Name);
            if (!string.Equals(requested.Name, "UnityEngine.CoreModule", StringComparison.Ordinal))
            {
                return null;
            }

            return unityCoreModuleShim ?? (unityCoreModuleShim = BuildUnityCoreModuleShim());
        }

        private static Assembly BuildUnityCoreModuleShim()
        {
            var assemblyName = new AssemblyName("UnityEngine.CoreModule");
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("UnityEngine.CoreModule");

            var audioTypeBuilder = moduleBuilder.DefineEnum("UnityEngine.AudioType", TypeAttributes.Public, typeof(int));
            audioTypeBuilder.DefineLiteral("WAV", 0);
            audioTypeBuilder.CreateType();

            return assemblyBuilder;
        }
    }
}
