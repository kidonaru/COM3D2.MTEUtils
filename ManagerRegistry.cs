using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace COM3D2.MotionTimelineEditor
{
    public interface IManager
    {
        void Init();
        void PreUpdate();
        void Update();
        void LateUpdate();
        void OnLoad();
        void OnPluginDisable();
        void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode);
    }

    public class ManagerRegistry
    {
        private readonly List<IManager> _managers = new List<IManager>();

        private static ManagerRegistry _instance;
        public static ManagerRegistry instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ManagerRegistry();
                }
                return _instance;
            }
        }

        public void RegisterManager(IManager manager)
        {
            _managers.Add(manager);
            manager.Init();
        }

        public void Update()
        {
            foreach (var manager in _managers)
            {
                try
                {
                    manager.Update();
                }
                catch (Exception e)
                {
                    MTEUtils.LogException(e);
                }
            }
        }

        public void LateUpdate()
        {
            foreach (var manager in _managers)
            {
                try
                {
                    manager.LateUpdate();
                }
                catch (Exception e)
                {
                    MTEUtils.LogException(e);
                }
            }
        }

        public void OnLoad()
        {
            foreach (var manager in _managers)
            {
                try
                {
                    manager.OnLoad();
                }
                catch (Exception e)
                {
                    MTEUtils.LogException(e);
                }
            }
        }

        public void OnPluginDisable()
        {
            foreach (var manager in _managers)
            {
                try
                {
                    manager.OnPluginDisable();
                }
                catch (Exception e)
                {
                    MTEUtils.LogException(e);
                }
            }
        }

        public void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
            foreach (var manager in _managers)
            {
                try
                {
                    manager.OnChangedSceneLevel(scene, sceneMode);
                }
                catch (Exception e)
                {
                    MTEUtils.LogException(e);
                }
            }
        }
    }
}