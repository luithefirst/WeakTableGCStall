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

            private readonly Dictionary<string, float> m_sourceValues = new Dictionary<string, float>()
            {
                { gcTime, 0.0f },
                { gen0, 0.0f },
                { gen1, 0.0f },
                { gen2, 0.0f },
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
                Console.WriteLine(String.Format(CultureInfo.InvariantCulture, "[GC] Time={0:0.0}% Gen0={1} Gen1={2} Gen2={3}", m_sourceValues[gcTime], m_sourceValues[gen0], m_sourceValues[gen1], m_sourceValues[gen2]));
            }
        }
    }
}
