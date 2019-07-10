using System;
using System.Diagnostics;

namespace CharacterBuilder
{
    public class CodeTimer : IDisposable
    {
        private readonly Stopwatch m_Stopwatch;
        private readonly string m_Text;
        public CodeTimer(string text)
        {
            m_Text = text;
            m_Stopwatch = Stopwatch.StartNew();
        }
        public void Dispose()
        {
            m_Stopwatch.Stop();
            var message = string.Format("Profiled {0}: {1:0.00}ms", m_Text, m_Stopwatch.ElapsedMilliseconds);
            Main.Log(message);
        }
    }
}