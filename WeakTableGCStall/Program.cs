using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace WeakTableGCStall
{
    class Program
    {
        static void Main(string[] args)
        {
            GCMonitor.Register();

            var programData = new List<object>(10000000);
            var computationTable = new ConditionalWeakTable<object, object>();

            var runtime = Stopwatch.StartNew();
            var garbageTime = Stopwatch.StartNew();
            var updateTime = Stopwatch.StartNew();
            var avgTime = new MedianWindow(20);
            var weakTableEntries = 0;
            var iteration = 0;
            long sum = 0;
            while (runtime.ElapsedMilliseconds < 1000 * 100) // run for 100s
            {
                // periodically increase number of weak table entries
                if (garbageTime.ElapsedMilliseconds > 1000) // increase number of weak table entries
                {
                    for (int k = 0; k < 10000; k++)
                    {
                        var go = new object();
                        programData.Add(go);
                        computationTable.Add(go, new object()); // comment this to see the behavior when not using ConditionalWeakTable entries
                    }
                    weakTableEntries += 10000;

                    Console.WriteLine(String.Format("WeakTable Entries: {0}", weakTableEntries));

                    garbageTime.Restart();
                }

                // do some work that creates some gc objects
                updateTime.Restart();

                for (int i = 0; i < 20; i++)
                {
                    var buffer = new int[200];
                    for (int k = 0; k < buffer.Length; k++)
                        buffer[k] = k * k;
                    sum += buffer.Min();
                }

                var time = updateTime.Elapsed.TotalSeconds * 1e6; // to micro seconds
                avgTime.Insert(time);

                // check if computation time is a significant outlier
                if (iteration > 100)
                {
                    if (time / avgTime.Value > 100)
                    {
                        var gcInfo = GC.GetGCMemoryInfo(GCKind.Ephemeral);
                        Console.WriteLine(String.Format(CultureInfo.InvariantCulture, 
                            "Update Time Outlier (Iteration={0}): {1:0.0}ms (x{2:0}, Mean={3}us) GCPause: {4:0.0}ms GCIndex={5}", 
                            iteration, 
                            time / 1000, 
                            time / avgTime.Value, 
                            (int)avgTime.Value, 
                            gcInfo.PauseDurations[0].TotalMilliseconds,
                            gcInfo.Index));
                    }
                }

                iteration++;
            }

            Console.WriteLine(sum.ToString());
        }
    }

    public class MedianWindow
    {
        double m_median = 0;
        int m_write = -1;
        int m_count = 0;
        double[] m_buffer;
        double[] m_sorted;

        public MedianWindow(int count)
        {
            m_buffer = new double[count];
            m_sorted = new double[count];
        }

        public double Insert(double value)
        {
            if (m_count < m_buffer.Length)
                m_count++;

            if (m_write > m_buffer.Length - 2)
                m_write = 0;
            else
                m_write++;

            m_buffer[m_write] = value;

            for (int i = 0; i < m_count; i++)
                m_sorted[i] = m_buffer[i];
            Array.Sort(m_sorted, 0, m_count);
            m_median = m_sorted[m_count / 2];

            return m_median;
        }

        public double Value { get { return m_median; } }
    }
}
