#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Inventory;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.UI.Game.Presenters;
using UnityEngine;

namespace AbsoluteZero.UI.Game
{
    public sealed class CombatRoleCaptureProbe : MonoBehaviour
    {
        int _index;
        uint _cue;
        void OnEnable() => CombatVFXManager.OnItemPresentationCue += Cue;
        void OnDisable() => CombatVFXManager.OnItemPresentationCue -= Cue;
        void Cue(int actor, int target, string item, string stage)
        {
            if (stage == "item-start" || stage == "item-end") _cue++;
            if (stage == "item-start" || stage == "cat-rummage" || stage == "item-end")
                StartCoroutine(Sample(actor, target, item, stage, _cue));
        }
        IEnumerator Sample(int actor, int target, string item, string stage, uint cue)
        {
            yield return new WaitForSeconds(stage == "cat-rummage" ? .35f : .2f);
            if (cue != _cue) yield break;
            yield return new WaitForEndOfFrame();
            var presenter = GetComponent<CombatRoleIconPresenter>();
            bool expected = stage != "item-end" && item != "Warm Tea";
            var localPlayer = InventoryPresenter.Instance?.GetLocalPlayer();
            int localSeat = localPlayer != null ? localPlayer.PlayerIndex : -1;
            bool expectedAttacker = expected && actor != localSeat;
            bool expectedTarget = expected && target != localSeat;
            bool roles = localSeat >= 0 && presenter != null
                && presenter.AttackerVisible == expectedAttacker && presenter.TargetVisible == expectedTarget;
            Debug.Log($"[ROLECAT] {(roles ? "PASS" : "FAIL")} roles {item} {stage} local={localSeat} actor={actor} target={target} expectedAttacker={expectedAttacker} expectedTarget={expectedTarget} attacker={presenter?.AttackerVisible} recipient={presenter?.TargetVisible}");
            bool thermometerLayout = GameObject.Find("MyTemp") == null && GameObject.Find("OppTemp") == null;
            foreach (string iconName in new[] { "AttackerRoleIcon", "RecipientRoleIcon" })
            {
                var icon = GameObject.Find(iconName);
                if (icon != null)
                    thermometerLayout &= icon.transform.parent != null && icon.transform.parent.name == "HPBar"
                        && icon.GetComponent<UnityEngine.UI.Image>() != null;
            }
            Debug.Log($"[ROLECAT] {(thermometerLayout ? "PASS" : "FAIL")} thermometer-children-and-no-temperature-labels {stage}");
            if (stage == "cat-rummage")
            {
                var cat = GameObject.Find("CatAnim") ?? GameObject.Find("CatAnimTemp");
                var inventory = InventoryPresenter.Instance;
                int local = inventory.GetLocalPlayer().PlayerIndex;
                Vector3 a = default, b = default;
                bool found;
                if (target == local || MatchCompositionRoot.Instance.ActiveConfig.RequiredPlayerCount == 2)
                {
                    var views = MatchCompositionRoot.Instance.ViewBindings;
                    var first = views.GetItemAnchor(target == local, 0);
                    var last = views.GetItemAnchor(target == local, 7);
                    found = first != null && last != null;
                    if (found) { a = first.position; b = last.position; }
                }
                else found = inventory.GetComponent<MultiPerspectiveLayout>().TryGetItemSweep(target, out a, out b);
                float distance = 999;
                if (found && cat != null)
                {
                    Vector3 line = b - a;
                    Vector3 nearest = a + line * Mathf.Clamp01(Vector3.Dot(cat.transform.position - a, line) / Mathf.Max(.0001f, line.sqrMagnitude));
                    distance = Vector3.ProjectOnPlane(cat.transform.position - nearest, Vector3.up).magnitude;
                    if (MatchCompositionRoot.Instance.ActiveConfig.RequiredPlayerCount > 2)
                    {
                        var sprite = cat.GetComponent<SpriteRenderer>();
                        float bottom = sprite != null ? sprite.bounds.min.y : float.MinValue;
                        Debug.Log($"[ROLECAT] {(bottom >= a.y + .03f ? "PASS" : "FAIL")} cat-clearance bottom={bottom:F4} plane={a.y:F4}");
                    }
                }
                Debug.Log($"[ROLECAT] {(distance < .08f ? "PASS" : "FAIL")} cat-rack distance={distance:F4} local={local} target={target} cat={cat?.transform.position} first={a} last={b}");
            }
            if (stage == "cat-rummage" || (stage == "item-start" && actor == 0))
            {
                string file = Path.Combine(Path.GetDirectoryName(Application.consoleLogPath), Path.GetFileNameWithoutExtension(Application.consoleLogPath)
                    + $".role-{++_index:00}-{item.Replace(' ', '-')}-{stage}.png");
                ScreenCapture.CaptureScreenshot(file);
                Debug.Log("[ROLECAT] CAPTURE " + file);
            }
        }
    }
}
#endif
