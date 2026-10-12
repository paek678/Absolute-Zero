#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.UI.Game.Bridge;
using UnityEngine;

namespace AbsoluteZero.UI.Game
{
    // Explicit validation only. Changes the process's in-memory test name, never PlayerPrefs.
    public sealed class Plan047HudProbe : MonoBehaviour
    {
        string _last; int _captures, _mismatchFrames;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--plan047-check") < 0) return;
            var go = new GameObject(nameof(Plan047HudProbe)); DontDestroyOnLoad(go); go.AddComponent<Plan047HudProbe>();
        }
        IEnumerator Start()
        {
            while (CosmeticProfileService.Instance == null) yield return null;
            string role = Path.GetFileNameWithoutExtension(Application.consoleLogPath);
            string name = role == "host" ? "호스트" : role.StartsWith("client") ? "참가자" + role.Substring(6) : "솔로검증";
            typeof(CosmeticProfileService).GetField("_nickname", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(CosmeticProfileService.Instance, name);
            var wait = new WaitForSecondsRealtime(.25f);
            while (true)
            {
                yield return wait;
                var bridge = FindFirstObjectByType<GameDataBridge>();
                if (bridge == null || bridge.SeatCount < 2) continue;
                var state = bridge.CurrentPresentation;
                var players = FindObjectsByType<PlayerState>(FindObjectsSortMode.None).Where(p => p.IsSpawned && p.IsParticipantReady).OrderBy(p => p.PlayerIndex).ToArray();
                // The bridge batches NV changes in LateUpdate. A same-frame read before that flush is not a desync.
                bool mismatch = players.Any(p => bridge.TryGetSeat((byte)p.PlayerIndex, out var seat) && seat.DisplayName != p.DisplayNameText);
                if (mismatch)
                {
                    if (++_mismatchFrames == 8) Debug.LogError("[BANNER] Name mismatch persisted for two seconds");
                    continue;
                }
                _mismatchFrames = 0;
                string roster = string.Join(";", players.Select(p => p.PlayerIndex + ":" + p.DisplayNameText));
                string key = roster + " seq=" + state.Sequence + " first=" + (state.Active ? state.FirstSeat : -1) + " current=" + (state.Active ? state.CurrentSeat : -1);
                if (_last == key) continue;
                _last = key;
                Debug.Log("[BANNER] " + key);
                if (_captures++ < 8)
                {
                    string path = Path.Combine(Path.GetDirectoryName(Application.consoleLogPath), Path.GetFileNameWithoutExtension(Application.consoleLogPath) + ".banner-" + _captures + ".png");
                    SoloLaunchProbe.WriteRender(path);
                }
            }
        }
    }
}
#endif
