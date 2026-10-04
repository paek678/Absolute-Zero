#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Cosmetic;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    // Opt-in observer only. Does not select outfits, drive turns or write saved/network state.
    [DefaultExecutionOrder(20000)]
    public sealed class AppearanceValidationProbe : MonoBehaviour
    {
        [Serializable] sealed class Evidence
        { public int samples; public List<string> observations = new(), failures = new(), captures = new(); }
        readonly Evidence _evidence = new();
        readonly HashSet<string> _seen = new();
        readonly HashSet<int> _toggled = new();
        readonly HashSet<int> _ghostReplayed = new();
        bool _toggle;
        string _path;
        float _next;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args,"--az-appearance-evidence");
            if (at < 0 || at+1 >= args.Length) return;
            var go = new GameObject(nameof(AppearanceValidationProbe)); DontDestroyOnLoad(go);
            var probe=go.AddComponent<AppearanceValidationProbe>();probe._path = Path.GetFullPath(args[at+1]);
            probe._toggle=Array.IndexOf(args,"--az-appearance-toggle")>=0;
        }
        void LateUpdate()
        {
            if (_path == null || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + .25f;
            foreach (var p in FindObjectsByType<PlayerState>(FindObjectsSortMode.None))
            {
                var v=p.GetComponent<AZPlayerVisual>(); var root=v?.GetVisualRoot();
                if (!p.IsSpawned || v == null || !v.IsPresentationReady || root == null || v.IsLocalHuman) continue;
                var report=v.AppearanceReport;if(report==null)continue;
                if(_toggle && _toggled.Add(root.GetInstanceID()))
                {
                    root.gameObject.SetActive(false);root.gameObject.SetActive(true);
                    report=v.AppearanceReport;
                    if(report==null||!report.Success)Fail("Visual reactivation failed for seat "+p.PlayerIndex);
                    else _evidence.observations.Add("SetActive replay passed: seat="+p.PlayerIndex+" profile="+report.Profile);
                }
                if(report==null) { Save(); continue; }
                if (_toggle && v.IsGhost && _ghostReplayed.Add(root.GetInstanceID()))
                {
                    string retained=p.CosmeticDataNV.Value.ToString();
                    if (CosmeticCodec.TryParse(retained,out var dto))
                    {
                        // Presentation-only delayed snapshot fixture; authoritative NV/save stay unchanged.
                        dto.back=dto.back=="back_01"?"back_02":"back_01";
                        v.ApplyRemoteCosmetic(JsonUtility.ToJson(dto));
                        var changed=v.AppearanceReport;
                        if(changed==null||!changed.Success||root.GetComponentsInChildren<SpriteRenderer>(true)
                            .Any(r=>r.name=="_cosmetic_atlas_layer"&&r.gameObject.activeSelf&&r.enabled))
                            Fail("Ghost delayed presentation exposed equipment for seat "+p.PlayerIndex);
                        else _evidence.observations.Add("Ghost delayed presentation hidden: seat="+p.PlayerIndex);
                        v.ApplyRemoteCosmetic(retained);report=v.AppearanceReport;
                    }
                }
                _evidence.samples++;
                string prefix="seat="+p.PlayerIndex+" object="+p.NetworkObjectId+" life="+p.CurrentLifeState.Value+" ids="+p.CosmeticDataNV.Value;
                if (!report.Success) Fail(prefix+" apply="+report.Error);
                var head=root.Find("body/head")?.GetComponent<SpriteRenderer>();
                if(p.IsBotControlled && report.ProfileVersion>0 && head!=null && head.sprite!=null && !head.sprite.name.StartsWith("robot_",StringComparison.Ordinal))
                    Fail(prefix+" nonRobotHead="+head.sprite.name);
                var layers=root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="_cosmetic_atlas_layer" && r.gameObject.activeSelf).ToArray();
                if(v.IsGhost && layers.Any(r=>r.enabled))Fail(prefix+" visible ghost equipment");
                if(layers.Length!=report.LayerCount)Fail(prefix+" layer count mismatch");
                string key=prefix+" profile="+report.Profile+" head="+head?.sprite?.name+" layers="+layers.Length
                    +" visibleGhost="+v.IsGhost+" enabledLayers="+layers.Count(r=>r.enabled);
                if(_seen.Add(key))
                {
                    _evidence.observations.Add(key);
                    if(_evidence.captures.Count<24 && head!=null && (head.enabled || v.IsGhost))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_path));
                        string capture=Path.ChangeExtension(_path,null)+"-"+_evidence.captures.Count+".png";
                        try { SoloLaunchProbe.WriteRender(capture); _evidence.captures.Add(capture); }
                        catch (Exception e) { Fail("capture: "+e.Message); }
                    }
                    Save();
                }
            }
        }
        void Fail(string message) { if(!_evidence.failures.Contains(message))_evidence.failures.Add(message); }
        void Save() { if(_path==null)return;Directory.CreateDirectory(Path.GetDirectoryName(_path));File.WriteAllText(_path,JsonUtility.ToJson(_evidence,true)); }
        void OnApplicationQuit() => Save();
    }
}
#endif
