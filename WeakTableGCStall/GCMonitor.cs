using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;

namespace WeakTableGCStall
{
    /// <summary>
    /// Monitor for GC activity. Only works when config is set to gcServer=true
    /// </summary>
    public static class GCMonitor
    {
        static IDisposable _listenerDispose = null;

        public static bool IsRegistered { get { return _listenerDispose != null; } }

        /// <summary>
        /// Timing interval in seconds
        /// </summary>
        public static int TimingInterval { get; set; } = 1;

        public static IDisposable Register()
        {
            if (IsRegistered) throw new InvalidOperationException("Already registered!");

            var listener = new GCStatsEventListener();

            Console.WriteLine("[GC] Registered GC notification.");

            return listener;
        }

        public static void Unregister()
        {
            if (!IsRegistered) throw new InvalidOperationException("Not Registered!");
            _listenerDispose.Dispose();
        }

        class GCStatsEventListener : EventListener
        {
            static readonly string gcTime = "time-in-gc";
            static readonly string gen0 = "gen-0-gc-count";
            static readonly string gen1 = "gen-1-gc-count";
            static readonly string gen2 = "gen-2-gc-count";
            static readonly string allocRate = "alloc-rate";
            static readonly string gen0Size = "gen-0-size";
            static readonly string gen1Size = "gen-1-size";
            static readonly string gen2Size = "gen-2-size";
            static readonly string lohSize = "loh-size";

            private readonly Dictionary<string, float> m_sourceValues = new Dictionary<string, float>()
            {
                { gcTime, 0.0f },
                { gen0, 0.0f },
                { gen1, 0.0f },
                { gen2, 0.0f },
                { allocRate, 0.0f },
            };

            int m_updateCnt = 0;
            int m_printInterval = 4;

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name.Equals("System.Runtime"))
                {
                    EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All, new Dictionary<string, string>
                    {
                        { "EventCounterIntervalSec", TimingInterval.ToString()}
                    });
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                foreach (var payload in eventData.Payload)
                {
                    if (!(payload is IDictionary<string, object> eventPayload))
                        continue;
                    
                    if (!eventPayload.TryGetValue("Name", out var counterName))
                        continue;

                    if (!eventPayload.TryGetValue("CounterType", out var counterType))
                        continue;

                    var counterNameString = (string)counterName;
                    if (!m_sourceValues.ContainsKey(counterNameString))
                        continue;

                    var value = 0.0f;
                    if (counterType.Equals("Sum") && eventPayload.TryGetValue("Increment", out var increment)) // use "Increment" to get delta
                        value = Convert.ToSingle(increment);
                    else if (counterType.Equals("Mean") && eventPayload.TryGetValue("Mean", out var mean))
                        value = Convert.ToSingle(mean);
                    else
                        continue;

                    m_sourceValues[counterNameString] = value;
                    m_updateCnt++;
                }

                if (m_updateCnt == m_printInterval)
                {
                    PrintValues();
                    m_updateCnt = 0;
                }

            }

            void PrintValues()
            {
                Console.WriteLine(
                    String.Format(
                        CultureInfo.InvariantCulture, 
                        "[GC] Time={0:0.0}% Alloc={1:#}/s Gen0={2} Gen1={3} Gen2={4}", 
                        m_sourceValues[gcTime], 
                        FormatMemory((long)m_sourceValues[allocRate]), 
                        m_sourceValues[gen0], 
                        m_sourceValues[gen1], 
                        m_sourceValues[gen2]));
            }

            string FormatMemory(long mem)
            {
                var l2 = Math.Log2(Math.Max(1, mem));
                var l2M = (int)((l2 - 4) / 10);
                var shift = Math.Clamp(l2M, 0, 3);
                var value = mem >> (shift * 10);
                var unt = shift == 0 ? "B " :
                          shift == 1 ? "KB" :
                          shift == 2 ? "MB" : "GB";

                return value.ToString("N0") + unt;
            }
        }
    }
}
