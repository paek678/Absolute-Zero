using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Core.Session
{
    public sealed class SceneTransitionService : IAsyncSceneTransitionService
    {
        readonly string _titleSceneName;
        AsyncOperation _pendingLoad;

        public SceneTransitionService(string titleSceneName)
        {
            _titleSceneName = titleSceneName;
        }

        public void LoadTitleScene()
        {
            SceneManager.LoadScene(_titleSceneName);
            Debug.Log($"[SceneTransition] Loaded: {_titleSceneName}");
        }

        public async Task LoadTitleSceneAsync()
        {
            if (_pendingLoad != null && _pendingLoad.isDone) _pendingLoad = null;
            if (_pendingLoad == null)
            {
                if (SceneManager.GetActiveScene().name == _titleSceneName) return;
                _pendingLoad = SceneManager.LoadSceneAsync(_titleSceneName, LoadSceneMode.Single);
                if (_pendingLoad == null) throw new InvalidOperationException("Could not start the menu scene load");
            }
            double deadline = Time.realtimeSinceStartupAsDouble + 35;
            while (!_pendingLoad.isDone)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                    throw new TimeoutException("Menu scene load is still pending; cleanup must retain session ownership");
                await Task.Delay(20);
            }
            _pendingLoad = null;
            Debug.Log($"[SceneTransition] Loaded: {_titleSceneName}");
        }
    }
}
