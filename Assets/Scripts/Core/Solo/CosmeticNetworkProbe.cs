#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public sealed class CosmeticNetworkProbe : MonoBehaviour
    {
        string _id;
        string _topId;
        int _count = 4;
        bool _fullSet;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (Application.isEditor) return;
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--az-cosmetic-id");
            if (at < 0 || at + 1 >= args.Length) return;
            var go = new GameObject(nameof(CosmeticNetworkProbe)); DontDestroyOnLoad(go);
            var probe = go.AddComponent<CosmeticNetworkProbe>();
            probe._id = args[at + 1];
            int topAt = Array.IndexOf(args, "--az-cosmetic-top");
            if (topAt >= 0 && topAt + 1 < args.Length) probe._topId = args[topAt + 1];
            int countAt = Array.IndexOf(args, "--az-cosmetic-count");
            if (countAt >= 0 && countAt + 1 < args.Length && int.TryParse(args[countAt + 1], out int count) && count >= 2 && count <= 4)
                probe._count = count;
            probe._fullSet = Array.IndexOf(args, "--az-cosmetic-full-set") >= 0;
        }
        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 90;
            while (CosmeticProfileService.Instance?.Registry == null && Time.realtimeSinceStartup < deadline) yield return null;
            var service = CosmeticProfileService.Instance;
            var item = service?.Registry?.GetById(_id);
            if (item == null) { Debug.LogError("[COSMETIC] FAIL missing catalog ID " + _id); yield break; }
            // Session-local fixture only: never Save or modify the user's stored profile.
            foreach (CosmeticPart part in Enum.GetValues(typeof(CosmeticPart))) service.EquipState.Unequip(part);
            service.EquipState.Equip(item);
            if (_fullSet)
                foreach (CosmeticPart part in Enum.GetValues(typeof(CosmeticPart)))
                {
                    if (part == CosmeticPart.Head) continue;
                    var supplied = service.Registry.GetByPart(part).FirstOrDefault(i => i.Id.EndsWith("_ref", StringComparison.Ordinal));
                    if (supplied != null) service.EquipState.Equip(supplied);
                }
            if (!string.IsNullOrEmpty(_topId))
            {
                var top = service.Registry.GetById(_topId);
                if (top == null || top.Part != CosmeticPart.Top) { Debug.LogError("[COSMETIC] FAIL missing top " + _topId); yield break; }
                service.EquipState.Equip(top);
            }
            var expectedLocal = service.Equipment.Snapshot;
            Debug.Log("[COSMETIC] EQUIPPED " + _id);
            while (Time.realtimeSinceStartup < deadline)
            {
                var players = FindObjectsByType<PlayerState>(FindObjectsSortMode.None).Where(p => p.IsSpawned).OrderBy(p => p.PlayerIndex).ToArray();
                if (players.Length != _count || players.Any(p => p.PlayerIndex < 0 || p.CosmeticDataNV.Value.IsEmpty)) { yield return null; continue; }
                bool ready = true;
                foreach (var player in players)
                {
                    var dto = JsonUtility.FromJson<CosmeticDto>(player.CosmeticDataNV.Value.ToString());
                    if (_topId != null && service.Registry.GetById(dto.top)?.Part != CosmeticPart.Top) { ready = false; break; }
                    if (_fullSet && (dto.top != expectedLocal.Top || dto.back != expectedLocal.Back
                        || dto.bottom != expectedLocal.Bottom || dto.tail != expectedLocal.Tail)) { ready = false; break; }
                    var equipped = service.Registry.GetById(dto.head);
                    if (equipped?.Atlas == null) { ready = false; break; }
                    if (player.IsOwner)
                    {
                        if (player.CosmeticSubmission != CosmeticSubmissionStatus.Accepted
                            && player.CosmeticSubmission != CosmeticSubmissionStatus.AcceptedLate) { ready = false; break; }
                        if (FPSVisualController.Instance == null || FPSVisualController.Instance.BoundHuman?.State != player)
                        { ready = false; break; }
                        if (!CosmeticCodec.SameIds(dto, expectedLocal.ToDto())) { ready = false; break; }
                        if (_fullSet && !MatchesSuppliedMappings(FPSVisualController.Instance.transform, dto, service.Registry, CosmeticView.FirstPerson))
                        { ready = false; break; }
                        continue;
                    }
                    var root = player.GetComponent<AZPlayerVisual>().GetVisualRoot();
                    var expected = equipped.Atlas.Bindings.First(b => b.Replacement != null).Replacement;
                    if (root == null || !root.GetComponentsInChildren<SpriteRenderer>(true)
                            .Any(r => r.gameObject.name == "_cosmetic_atlas_layer" && r.sprite == expected && r.enabled))
                    { ready = false; break; }
                    if ((_fullSet || _topId != null) && !MatchesSuppliedMappings(root, dto, service.Registry, CosmeticView.Character))
                    { ready = false; break; }
                }
                if (ready)
                {
                    string signature = string.Join(";", players.Select(p => p.PlayerIndex + ":" + ((_fullSet || _topId != null)
                        ? p.CosmeticDataNV.Value.ToString() : JsonUtility.FromJson<CosmeticDto>(p.CosmeticDataNV.Value.ToString()).head)));
                    if (players.Select(p => JsonUtility.FromJson<CosmeticDto>(p.CosmeticDataNV.Value.ToString()).head).Distinct().Count() != _count)
                        Debug.LogError("[COSMETIC] FAIL expected distinct hats for " + _count + " seats");
                    else
                    {
                        Debug.Log("[COSMETIC] VERIFIED " + signature);
                        Debug.Log("[COSMETIC] LOCAL_ACCEPTANCE " + players.First(p => p.IsOwner).CosmeticSubmission);
                        var local = players.First(p => p.IsOwner);
                        Debug.Log("[COSMETIC] SCOPE rootServerGeneration=" + AbsoluteZero.Core.Match.MatchCompositionRoot.Instance.Generation
                            + " serverParticipant=" + local.ParticipantMetadata.Value.Generation);
                    }
                    yield break;
                }
                yield return null;
            }
            Debug.LogError("[COSMETIC] FAIL timed out waiting for " + _count + " replicated and rendered outfits");
        }
        static bool MatchesSuppliedMappings(Transform root, CosmeticDto dto, CosmeticRegistrySO registry, CosmeticView view)
        {
            var snapshot = new CosmeticSnapshot(dto, 0);
            foreach (CosmeticPart part in Enum.GetValues(typeof(CosmeticPart)))
            {
                var item = registry.GetById(snapshot.Get(part));
                if (item?.Atlas == null) continue; // Missing optional artwork keeps the original.
                foreach (var binding in item.Atlas.Bindings)
                {
                    if (binding == null || binding.View != view || binding.Replacement == null) continue;
                    var target = root.Find(binding.RendererPath)?.GetComponent<SpriteRenderer>();
                    if (target == null) return false;
                    if (binding.Overlay)
                    {
                        if (binding.Source != null && target.sprite != binding.Source) continue;
                        if (!target.GetComponentsInChildren<SpriteRenderer>().Any(r => r != target
                            && r.enabled && r.sprite == binding.Replacement)) return false;
                    }
                    else if (target.sprite == binding.Source && binding.Source != binding.Replacement) return false;
                }
            }
            return true;
        }
    }
}
#endif
