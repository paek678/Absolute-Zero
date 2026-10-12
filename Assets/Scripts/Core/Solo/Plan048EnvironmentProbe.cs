#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Maps;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    // Read-only, opt-in evidence of actual loaded map and replicated environment.
    public sealed class Plan048EnvironmentProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--plan048-check") < 0) return;
            var go = new GameObject(nameof(Plan048EnvironmentProbe)); DontDestroyOnLoad(go); go.AddComponent<Plan048EnvironmentProbe>();
        }
        IEnumerator Start()
        {
            string last = null, lastFan = null; int captures = 0;
            bool checkFans = Array.IndexOf(Environment.GetCommandLineArgs(), "--az-fan-check") >= 0;
            var delay = new WaitForSecondsRealtime(.5f);
            while (true)
            {
                yield return delay;
                var root = MatchCompositionRoot.Instance;
                var turn = TurnManager.Instance;
                if (root == null || root.ActiveConfig == null || turn == null || !turn.IsSpawned) continue;
                var map = FindFirstObjectByType<MapSceneBinding>();
                var mode = root.ActiveConfig.Mode;
                if (checkFans)
                {
                    var visuals = FindObjectsByType<AZPlayerVisual>(FindObjectsSortMode.None)
                        .Where(v => v.IsPresentationReady && !v.IsLocalHuman).ToArray();
                    if (visuals.Length > 0)
                    {
                        int fans = FindObjectsByType<FanBladeSpinner>(FindObjectsSortMode.None).Length;
                        bool valid = fans == (mode == GameMode.Multi ? 0 : 2)
                            && visuals.All(v => v.GetAnimator().GetBool("isWind")
                                == (mode != GameMode.Multi && v.GetComponent<PlayerState>().IsFanActive.Value));
                        string fanKey = mode + " turn=" + turn.TurnNumber.Value + " phase=" + turn.CurrentPhase.Value
                            + " fans=" + fans + " remotes=" + visuals.Length
                            + " windy=" + visuals.Count(v => v.GetAnimator().GetBool("isWind"));
                        if (!valid) Debug.LogError("[FAN049] FAIL " + fanKey);
                        else if (fanKey != lastFan) Debug.Log("[FAN049] PASS " + fanKey);
                        lastFan = fanKey;
                    }
                }
                string expected = mode == GameMode.Multi ? "pavilion-night" : "pavilion-day";
                var env = turn.ActiveEnvironment.Value;
                if (mode == GameMode.Multi && map?.EnvironmentInstance != null && map.AppliedMap?.EnvironmentPrefab != null)
                {
                    var live = map.EnvironmentInstance.GetComponentsInChildren<Light>().FirstOrDefault(l => l.type == LightType.Directional);
                    var authored = map.AppliedMap.EnvironmentPrefab.GetComponentsInChildren<Light>().FirstOrDefault(l => l.type == LightType.Directional);
                    if (live != null && authored != null && (Mathf.Abs(live.intensity - authored.intensity) > .001f || live.color != authored.color))
                        Debug.LogError("[MAP048] FAIL night lighting changed env=" + env);
                }
                string key = mode + " map=" + (map?.AppliedMap?.Id ?? "missing") + " env=" + env;
                if (key == last) continue;
                last = key;
                bool banned = env == EnvironmentType.CicadaSong || (mode == GameMode.Multi
                    && (env == EnvironmentType.SunnyDay || env == EnvironmentType.HeatWaveWarning));
                if (map?.AppliedMap?.Id != expected || banned) Debug.LogError("[MAP048] FAIL " + key);
                else Debug.Log("[MAP048] PASS " + key);
                if (captures++ < 3)
                {
                    yield return new WaitForEndOfFrame();
                    string path = Path.Combine(Path.GetDirectoryName(Application.consoleLogPath), Path.GetFileNameWithoutExtension(Application.consoleLogPath) + ".map-" + captures + ".png");
                    SoloLaunchProbe.WriteRender(path);
                }
            }
        }
    }
}
#endif
