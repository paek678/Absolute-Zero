using UnityEngine;

namespace AbsoluteZero.Core.Session
{
    // Network shutdown still runs on exit; only new scene navigation is suppressed.
    internal static class ApplicationLifetime
    {
        static bool _exiting;

        public static bool CanNavigate
        {
            get
            {
                if (_exiting || !Application.isPlaying) return false;
#if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return false;
#endif
                return true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _exiting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

        static void OnQuitting() => _exiting = true;
#if UNITY_EDITOR
        static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) _exiting = true;
        }
#endif
    }
}
