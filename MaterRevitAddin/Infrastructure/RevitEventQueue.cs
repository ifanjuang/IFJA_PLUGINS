using System;
using System.Collections.Concurrent;
using Autodesk.Revit.UI;

namespace Mater2026.Infrastructure
{
    /// <summary>
    /// Simple queue to post small Revit API actions to the Revit thread (reduces boilerplate handlers).
    /// </summary>
    public static class RevitEventQueue
    {
        private class Runner : IExternalEventHandler
        {
            public void Execute(UIApplication app)
            {
                while (_q.TryDequeue(out var action))
                {
                    try { action(app); } catch { /* optionally log */ }
                }
            }
            public string GetName() => "Mater2026.RevitEventQueue";
        }

        private static readonly ConcurrentQueue<Action<UIApplication>> _q = new();
        private static readonly ExternalEvent _ev = ExternalEvent.Create(new Runner());

        public static void Post(Action<UIApplication> action)
        {
            if (action == null) return;
            _q.Enqueue(action);
            _ev.Raise();
        }
    }
}
