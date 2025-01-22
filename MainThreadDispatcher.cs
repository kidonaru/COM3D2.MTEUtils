using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private const int MaxActionsPerUpdate = 10;

        private static MainThreadDispatcher _instance;
        private static readonly Queue<Action> _actionQueue = new Queue<Action>();
        private static readonly object _lockObject = new object();

        public static void Initialize()
        {
            if (_instance == null)
            {
                var go = new GameObject("MainThreadDispatcher");
                _instance = go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
        }

        public static void Enqueue(Action action)
        {
            if (action == null) return;

            Initialize();

            lock (_lockObject)
            {
                _actionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            List<Action> actionsToRun = new List<Action>();

            lock (_lockObject)
            {
                while (_actionQueue.Count > 0 && actionsToRun.Count < MaxActionsPerUpdate)
                {
                    actionsToRun.Add(_actionQueue.Dequeue());
                }
            }

            foreach (var action in actionsToRun)
            {
                action?.Invoke();
            }
        }
    }
}