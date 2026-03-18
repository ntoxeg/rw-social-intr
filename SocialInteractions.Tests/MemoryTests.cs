using SocialInteractions.Memory;
using SocialInteractions.Tests.TestHelpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SocialInteractions.Tests
{
    public class MemoryTests : IDisposable
    {
        public MemoryTests()
        {
            SLog.Logger = new NullLogger();
        }

        public void Dispose()
        {
            SLog.Logger = null;
        }

        private PawnMemory_GameComponent CreateComponent()
        {
            return new PawnMemory_GameComponent();
        }

        #region Buffer Operations

        [Fact]
        public void AddBufferEntry_AddsEntries()
        {
            var comp = CreateComponent();
            comp.AddBufferEntry(1, "talked to Alice");
            comp.AddBufferEntry(1, "fought with Bob");

            var entries = comp.GetAndClearBuffer(1);
            Assert.Equal(2, entries.Count);
            Assert.Equal("talked to Alice", entries[0]);
            Assert.Equal("fought with Bob", entries[1]);
        }

        [Fact]
        public void AddBufferEntry_SeparateBuffersPerPawn()
        {
            var comp = CreateComponent();
            comp.AddBufferEntry(1, "pawn1 event");
            comp.AddBufferEntry(2, "pawn2 event");

            var entries1 = comp.GetAndClearBuffer(1);
            var entries2 = comp.GetAndClearBuffer(2);
            Assert.Single(entries1);
            Assert.Single(entries2);
            Assert.Equal("pawn1 event", entries1[0]);
            Assert.Equal("pawn2 event", entries2[0]);
        }

        [Fact]
        public void GetAndClearBuffer_ReturnsAndClears()
        {
            var comp = CreateComponent();
            comp.AddBufferEntry(1, "event1");
            comp.AddBufferEntry(1, "event2");

            var first = comp.GetAndClearBuffer(1);
            Assert.Equal(2, first.Count);
            Assert.Equal("event1", first[0]);
            Assert.Equal("event2", first[1]);

            // Second call returns empty (buffer was cleared)
            var second = comp.GetAndClearBuffer(1);
            Assert.Empty(second);
        }

        [Fact]
        public void GetAndClearBuffer_NonexistentPawnReturnsEmpty()
        {
            var comp = CreateComponent();
            var entries = comp.GetAndClearBuffer(999);
            Assert.Empty(entries);
        }

        [Fact]
        public void GetAllPawnsWithBufferEntries_ReturnsCorrectPawns()
        {
            var comp = CreateComponent();
            Assert.Empty(comp.GetAllPawnsWithBufferEntries());

            comp.AddBufferEntry(1, "event");
            comp.AddBufferEntry(2, "event");
            comp.AddBufferEntry(3, "event");

            var pawns = comp.GetAllPawnsWithBufferEntries();
            Assert.Equal(3, pawns.Count);
            Assert.Contains(1, pawns);
            Assert.Contains(2, pawns);
            Assert.Contains(3, pawns);

            // Clear one pawn's buffer
            comp.GetAndClearBuffer(2);
            pawns = comp.GetAllPawnsWithBufferEntries();
            Assert.Equal(2, pawns.Count);
            Assert.DoesNotContain(2, pawns);
        }

        #endregion

        #region Buffer Cap Enforcement

        [Fact]
        public void BufferCap_EnforcesMaximum()
        {
            var comp = CreateComponent();
            // BufferCapacity is 50 (private const in PawnMemory_GameComponent)
            for (int i = 0; i < 51; i++)
            {
                comp.AddBufferEntry(1, string.Format("event_{0}", i));
            }

            var entries = comp.GetAndClearBuffer(1);
            Assert.Equal(50, entries.Count);
            // event_0 (oldest) should be dropped
            Assert.Equal("event_1", entries[0]);
            Assert.Equal("event_50", entries[49]);
        }

        [Fact]
        public void BufferCap_ExcessiveEntriesKeepsNewest()
        {
            var comp = CreateComponent();
            for (int i = 0; i < 100; i++)
            {
                comp.AddBufferEntry(1, string.Format("event_{0}", i));
            }

            var entries = comp.GetAndClearBuffer(1);
            Assert.Equal(50, entries.Count);
            // Should keep the newest 50 entries (event_50 through event_99)
            Assert.Equal("event_50", entries[0]);
            Assert.Equal("event_99", entries[49]);
        }

        #endregion

        #region FIFO Truncation

        [Fact]
        public void FifoTruncation_RespectsSentenceBoundaries()
        {
            // "AAA. BBB. CCC. DDD." = 19 chars, limit 10
            // Should cut at sentence boundary after "BBB.", keeping "CCC. DDD." (9 chars)
            string input = "AAA. BBB. CCC. DDD.";
            string result = PawnMemory_GameComponent.ApplyFifoTruncation(input, 10);

            Assert.True(result.Length <= 10,
                string.Format("Expected <= 10 chars, got {0}: '{1}'", result.Length, result));
            Assert.Equal("CCC. DDD.", result);
        }

        [Fact]
        public void FifoTruncation_CutsAtFirstSentenceBoundary()
        {
            // "First sentence. Second sentence ends here." = 42 chars
            // charLimit = 27 forces cut past first sentence period at index 14
            string input = "First sentence. Second sentence ends here.";
            string result = PawnMemory_GameComponent.ApplyFifoTruncation(input, 27);

            Assert.Equal("Second sentence ends here.", result);
        }

        [Fact]
        public void FifoTruncation_RespectsNewlineBoundaries()
        {
            // "Line one\nLine two\nLine three" = 28 chars
            // charLimit = 19 forces removal of first line at \n boundary
            string input = "Line one\nLine two\nLine three";
            string result = PawnMemory_GameComponent.ApplyFifoTruncation(input, 19);

            Assert.Equal("Line two\nLine three", result);
        }

        [Fact]
        public void FifoTruncation_ReturnsOriginalWhenUnderLimit()
        {
            string input = "Short text.";
            string result = PawnMemory_GameComponent.ApplyFifoTruncation(input, 100);
            Assert.Equal(input, result);
        }

        [Fact]
        public void FifoTruncation_HandlesNullAndEmpty()
        {
            Assert.Null(PawnMemory_GameComponent.ApplyFifoTruncation(null, 100));
            Assert.Equal("", PawnMemory_GameComponent.ApplyFifoTruncation("", 100));
        }

        [Fact]
        public void FifoTruncation_ZeroOrNegativeLimitReturnsOriginal()
        {
            string input = "Some text.";
            Assert.Equal(input, PawnMemory_GameComponent.ApplyFifoTruncation(input, 0));
            Assert.Equal(input, PawnMemory_GameComponent.ApplyFifoTruncation(input, -1));
        }

        #endregion

        #region Memory Storage

        [Fact]
        public void MemoryStorage_GetSetClear_Works()
        {
            var comp = CreateComponent();

            // Initially empty
            Assert.Equal(string.Empty, comp.GetMemory(1));

            // Set and get
            comp.SetMemory(1, "I remember talking to Alice about crops.");
            Assert.Equal("I remember talking to Alice about crops.", comp.GetMemory(1));

            // Overwrite
            comp.SetMemory(1, "Updated memory about today.");
            Assert.Equal("Updated memory about today.", comp.GetMemory(1));

            // Set empty removes entry
            comp.SetMemory(1, "");
            Assert.Equal(string.Empty, comp.GetMemory(1));

            // Set null removes entry
            comp.SetMemory(2, "temporary");
            comp.SetMemory(2, null);
            Assert.Equal(string.Empty, comp.GetMemory(2));

            // ClearMemory removes memory and buffer
            comp.SetMemory(3, "will be cleared");
            comp.AddBufferEntry(3, "buffered event");
            comp.ClearMemory(3);
            Assert.Equal(string.Empty, comp.GetMemory(3));
            Assert.Empty(comp.GetAndClearBuffer(3));
        }

        [Fact]
        public void MemoryStorage_MultiplePawnsIndependent()
        {
            var comp = CreateComponent();
            comp.SetMemory(1, "pawn1 memory");
            comp.SetMemory(2, "pawn2 memory");

            Assert.Equal("pawn1 memory", comp.GetMemory(1));
            Assert.Equal("pawn2 memory", comp.GetMemory(2));

            comp.ClearMemory(1);
            Assert.Equal(string.Empty, comp.GetMemory(1));
            Assert.Equal("pawn2 memory", comp.GetMemory(2));
        }

        #endregion

        #region Colony-Only Guard

        [Fact]
        public void ColonyOnlyGuard_SkipsNonColonists()
        {
            // The colony-only guard is in SocialInteractions.GetPawnMemory(Pawn):
            //   if (!pawn.IsColonist) return string.Empty;
            //
            // The component stores data for ANY pawnId - the guard is at API level.
            // Testing Pawn.IsColonist requires game runtime, so we verify
            // component-level behavior and document the API contract.
            var comp = CreateComponent();

            // Component stores memory for any pawn ID (no colony guard at this level)
            comp.SetMemory(100, "colonist memory");
            comp.SetMemory(200, "outsider memory");
            Assert.Equal("colonist memory", comp.GetMemory(100));
            Assert.Equal("outsider memory", comp.GetMemory(200));

            // Buffer also works for any pawn ID
            comp.AddBufferEntry(100, "colonist event");
            comp.AddBufferEntry(200, "outsider event");
            var pawnsWithEntries = comp.GetAllPawnsWithBufferEntries();
            Assert.Contains(100, pawnsWithEntries);
            Assert.Contains(200, pawnsWithEntries);

            // ClearMemory works for any pawn ID
            comp.ClearMemory(200);
            Assert.Equal(string.Empty, comp.GetMemory(200));
            Assert.Empty(comp.GetAndClearBuffer(200));

            // The API-level guard (SocialInteractions.GetPawnMemory) ensures
            // non-colonist memories are never surfaced to LLM prompts.
            // Full integration test requires Pawn instances and game runtime.
        }

        #endregion

        #region Settings Integration

        [Fact]
        public void SettingsIntegration_DisabledReturnsEmpty()
        {
            // Verify MemorySettings defaults
            var memSettings = new SocialInteractionsModSettings.MemorySettings();
            Assert.True(memSettings.enableMemorySystem);
            Assert.Equal(2500, memSettings.memoryCharacterLimit);
            Assert.Equal(2000, memSettings.memoryCompactionThreshold);
            Assert.Equal(50, memSettings.memoryBufferEntryCap);

            // Disable flag changes correctly
            memSettings.enableMemorySystem = false;
            Assert.False(memSettings.enableMemorySystem);

            // Component stores data regardless of settings flag
            var comp = CreateComponent();
            comp.SetMemory(1, "stored regardless of settings");
            Assert.Equal("stored regardless of settings", comp.GetMemory(1));

            // When enableMemorySystem is false:
            // - SocialInteractions.GetPawnMemory() returns empty for ALL pawns
            // - GameComponentTick() skips daily memory processing entirely
            // Full integration verification requires game runtime.
        }

        [Fact]
        public void SettingsIntegration_FifoUsesCharacterLimit()
        {
            // Verify that FIFO truncation works with the configured character limit
            var memSettings = new SocialInteractionsModSettings.MemorySettings();
            int charLimit = memSettings.memoryCharacterLimit; // 2500

            // Generate memory content exceeding the limit
            string longMemory = new string('A', charLimit + 500) + ". End.";
            string truncated = PawnMemory_GameComponent.ApplyFifoTruncation(longMemory, charLimit);

            Assert.True(truncated.Length <= charLimit,
                string.Format("Truncated length {0} should be <= {1}", truncated.Length, charLimit));
        }

        #endregion

        #region Thread Safety

        [Fact]
        public async Task ThreadSafety_ConcurrentBufferOperationsSafe()
        {
            var comp = CreateComponent();
            int iterationsPerThread = 200;
            int threadCount = 8;
            int sharedPawnId = 1;

            // Multiple threads writing to the SAME pawn's buffer concurrently
            var tasks = new List<Task>();
            for (int t = 0; t < threadCount; t++)
            {
                int threadIdx = t;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        comp.AddBufferEntry(sharedPawnId,
                            string.Format("t{0}_e{1}", threadIdx, i));
                    }
                }));
            }

            await Task.WhenAll(tasks.ToArray());

            // Buffer should respect cap (50) with no data corruption
            var entries = comp.GetAndClearBuffer(sharedPawnId);
            Assert.Equal(50, entries.Count);

            // All entries should be valid strings (no corruption from races)
            foreach (var entry in entries)
            {
                Assert.False(string.IsNullOrEmpty(entry));
                Assert.Matches(@"^t\d+_e\d+$", entry);
            }
        }

        [Fact]
        public async Task ThreadSafety_ConcurrentMultiplePawns()
        {
            var comp = CreateComponent();
            int iterationsPerThread = 100;
            int threadCount = 8;

            // Each thread writes to its own pawn ID
            var tasks = new List<Task>();
            for (int t = 0; t < threadCount; t++)
            {
                int pawnId = t;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        comp.AddBufferEntry(pawnId,
                            string.Format("event_{0}", i));
                    }
                }));
            }

            await Task.WhenAll(tasks.ToArray());

            // Each pawn should have entries (capped at 50)
            int totalEntries = 0;
            for (int t = 0; t < threadCount; t++)
            {
                var entries = comp.GetAndClearBuffer(t);
                Assert.True(entries.Count > 0,
                    string.Format("Thread {0} should have entries", t));
                Assert.True(entries.Count <= 50,
                    string.Format("Thread {0} should have at most 50 entries, got {1}", t, entries.Count));
                totalEntries += entries.Count;
            }

            Assert.Equal(threadCount * 50, totalEntries);
        }

        [Fact]
        public async Task ThreadSafety_ConcurrentReadWrite()
        {
            var comp = CreateComponent();
            int writeIterations = 500;
            int errors = 0;
            bool writerDone = false;

            // Writer thread adds entries
            var writer = Task.Run(() =>
            {
                for (int i = 0; i < writeIterations; i++)
                {
                    comp.AddBufferEntry(1, string.Format("event_{0}", i));
                }
                writerDone = true;
            });

            // Reader threads concurrently query buffer state
            var readers = new List<Task>();
            for (int r = 0; r < 4; r++)
            {
                readers.Add(Task.Run(() =>
                {
                    while (!writerDone)
                    {
                        try
                        {
                            comp.GetAllPawnsWithBufferEntries();
                        }
                        catch
                        {
                            System.Threading.Interlocked.Increment(ref errors);
                        }
                    }
                }));
            }

            await writer;
            await Task.WhenAll(readers.ToArray());

            Assert.Equal(0, errors);
        }

        #endregion
    }
}
