#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    // Opt-in observation of actual presentation, including the rendered texture.
    public sealed class CombatAnimationProbe : MonoBehaviour
    {
        int _cue;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!Environment.GetCommandLineArgs().Contains("--az-animation-check")) return;
            var go = new GameObject(nameof(CombatAnimationProbe));
            DontDestroyOnLoad(go); go.AddComponent<CombatAnimationProbe>();
        }
        void OnEnable() => CombatVFXManager.OnItemPresentationCue += Cue;
        void OnDisable() => CombatVFXManager.OnItemPresentationCue -= Cue;
        void Cue(int actor, int target, string item, string stage)
        {
            string path = Path.ChangeExtension(Application.consoleLogPath, ".choreography.jsonl");
            File.AppendAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(new
                { actor, target, item, stage, time = Time.unscaledTime }) + "\n");
            if (stage == "item-end") { _cue++; return; }
            if (stage == "item-start") StartCoroutine(Observe(++_cue, actor, target, item));
        }
        IEnumerator Observe(int cue, int actor, int target, string item)
        {
            var data = ItemManager.Instance.GetAllItems().FirstOrDefault(x => x.ItemName == item);
            if (data == null) yield break;
            var expected = GameSprites.GetItemSpriteFor(data);
            var player = FindObjectsByType<PlayerState>(FindObjectsSortMode.None).FirstOrDefault(p => p.PlayerIndex == actor);
            var visual = player != null ? player.GetComponent<AZPlayerVisual>() : null;
            if (visual == null) yield break;
            var local = visual.IsLocalHuman;
            var root = local ? FPSVisualController.Instance?.transform : visual.GetVisualRoot();
            var animator = local ? root?.GetComponentInChildren<Animator>(true) : visual.GetAnimator();
            var renderer = local ? typeof(FPSVisualController).GetField("_itemRenderer", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(FPSVisualController.Instance) as SpriteRenderer
                : root?.Find("item")?.GetComponent<SpriteRenderer>();
            var props = new MaterialPropertyBlock();
            string prefix = Path.ChangeExtension(Application.consoleLogPath, ".animation");
            float start = Time.unscaledTime; int sample = 0;
            while (cue == _cue && Time.unscaledTime - start < 4)
            {
                yield return new WaitForSecondsRealtime(.1f);
                yield return new WaitForEndOfFrame();
                if (cue != _cue || renderer == null || animator == null) yield break;
                renderer.GetPropertyBlock(props);
                var texture = props.GetTexture("_MainTex");
                var clip = string.Join(",", animator.GetCurrentAnimatorClipInfo(0).Select(x => x.clip.name));
                var row = new { cue, actor, target, item, local, t=Time.unscaledTime-start, clip,
                    normalizedTime=animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
                    pose=root.GetComponentsInChildren<SpriteRenderer>(true)
                        .Where(x => x.name.StartsWith("arm") || x.name.StartsWith("hand") || x == renderer)
                        .Select(x => new { part=x.name, sprite=x.sprite?.name,
                            position=new[]{x.transform.localPosition.x,x.transform.localPosition.y,x.transform.localPosition.z},
                            rotation=new[]{x.transform.localEulerAngles.x,x.transform.localEulerAngles.y,x.transform.localEulerAngles.z} }).ToArray(),
                    expected=expected?.name, sprite=renderer.sprite?.name, active=renderer.gameObject.activeInHierarchy,
                    expectedTexture=expected?.texture?.name, texture=texture?.name,
                    spriteMatch=renderer.sprite == expected, textureMatch=texture == expected?.texture };
                File.AppendAllText(prefix + ".jsonl", Newtonsoft.Json.JsonConvert.SerializeObject(row) + "\n");
                ++sample;
                if ((sample == 2 || sample == 5 || sample == 8 || sample == 11) && actor == 0
                    && (item == "Fan" || item == "Warm Tea" || item == "Hand Fan"))
                    ScreenCapture.CaptureScreenshot(prefix + $"-{cue:00}-seat{actor}-{item.Replace(' ', '-')}-frame{sample:00}.png");
            }
        }
    }
}
#endif
