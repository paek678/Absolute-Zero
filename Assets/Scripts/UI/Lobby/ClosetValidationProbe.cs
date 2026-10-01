#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    // Opt-in standalone fixture. Uses a private UI copy and isolated persistence key.
    public sealed class ClosetValidationProbe : MonoBehaviour
    {
        [Serializable] sealed class Report { public bool passed; public string failure; public List<string> checks = new(); }
        string _mode, _directory, _key, _originalSave;
        bool _hadSave;
        readonly Report _report = new();
        GameObject _copy;
        ClosetView _view;
        ClosetPresenter _presenter;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (Application.isEditor) return;
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--plan037-closet");
            if (at < 0 || at + 3 >= args.Length) return;
            var go = new GameObject(nameof(ClosetValidationProbe)); DontDestroyOnLoad(go);
            var probe = go.AddComponent<ClosetValidationProbe>();
            probe._mode = args[at + 1]; probe._directory = args[at + 2];
            probe._key = "plan037_fixture_" + args[at + 3];
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(_directory);
            _hadSave = PlayerPrefs.HasKey(PlayerPrefsCosmeticStore.Key);
            _originalSave = PlayerPrefs.GetString(PlayerPrefsCosmeticStore.Key, "");
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object yielded = null; bool failed = false;
                try
                {
                    var flow = stack.Peek();
                    if (!flow.MoveNext()) { stack.Pop(); continue; }
                    yielded = flow.Current;
                    if (yielded is IEnumerator child) { stack.Push(child); continue; }
                }
                catch (Exception error) { _report.failure = error.ToString(); failed = true; }
                if (failed) break;
                yield return yielded;
            }
            _report.passed = _report.failure == null;
            _presenter?.Dispose(); _view?.Dispose();
            if (_copy != null) Destroy(_copy);
            if (_mode == "read") { PlayerPrefs.DeleteKey(_key); PlayerPrefs.Save(); }
            File.WriteAllText(Path.Combine(_directory, _mode + ".json"), JsonUtility.ToJson(_report, true));
            Debug.Log("[R11_CLOSET] " + (_report.passed ? "PASS " : "FAIL ") + _mode + " checks=" + _report.checks.Count + " " + _report.failure);
            Application.Quit(_report.passed ? 0 : 1);
        }
        void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException(name);
            _report.checks.Add(name);
        }
        IEnumerator Run()
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (CosmeticProfileService.Instance?.Registry == null || GameObject.Find("MainUI") == null)
            {
                Check(Time.realtimeSinceStartup < deadline, "App dependencies within budget");
                yield return null;
            }
            yield return null;
            var profile = CosmeticProfileService.Instance;
            var store = new FaultStore(new PlayerPrefsCosmeticStore(_key));
            var service = new CosmeticEquipmentService(new CosmeticEquipState(), profile.Registry, store);
            if (_mode == "read")
            {
                Check(service.Load().Success, "Read isolated persisted key in a new process");
                Check(service.Snapshot.Head == "hat_01", "Persisted equipped ID survived process restart");
            }
            else Check(!PlayerPrefs.HasKey(_key), "Fixture key is new");
            var canvas = GameObject.Find("MainUI").transform;
            var original = canvas.Find("ClosetPanel");
            Check(original != null, "Existing Closet panel found");
            while (AppBootstrapper.Instance?.IsReady != true) { Check(Time.realtimeSinceStartup < deadline, "Menu ready before input"); yield return null; }
            yield return Frames(3);
            var cameraMasks = Camera.allCameras.ToDictionary(c => c, c => c.cullingMask);
            var mainPanel = canvas.Find("MainPanel").gameObject;
            var mainClosetButton = mainPanel.transform.Find("ClosetBtn").GetComponent<Button>();
            var owner = FindFirstObjectByType<AZLobbyUI>();
            var navigation = (LobbyPresenter)typeof(AZLobbyUI).GetField("_presenter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(owner);
            mainClosetButton.onClick.Invoke(); yield return Frames(3);
            Check(original.gameObject.activeInHierarchy && !mainPanel.activeSelf, "Real Main button opens the production Closet");
            Check(original.GetComponent<ClosetPreviewSurface>().Texture != null, "Real menu entry renders preview");
            navigation.SetState(LobbyViewState.Room); yield return Frames(2);
            mainClosetButton.onClick.Invoke();
            Check(!original.gameObject.activeSelf, "Hidden Main button cannot open Closet from Room");
            navigation.SetState(LobbyViewState.Main); yield return Frames(2);
            Check(cameraMasks.All(p => p.Key == null || p.Key.cullingMask == p.Value), "Real menu exit restores scene camera masks");
            _copy = Instantiate(original.gameObject, canvas, false);
            _copy.name = "R11_PrivateClosetFixture";
            // The real menu entry has already populated reusable runtime cells. A
            // private fixture copy must start from the serialized template, not those
            // copied cells whose delegates belong to the original view instance.
            var copiedContent = _copy.GetComponent<ClosetViewBindings>().Content;
            foreach (Transform child in copiedContent.Cast<Transform>().ToArray())
            { child.gameObject.SetActive(false); child.SetParent(null); Destroy(child.gameObject); }
            foreach (var button in _copy.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            _view = new ClosetView(_copy);
            int closed = 0;
            _presenter = new ClosetPresenter(_view, () => service, snapshot =>
                NetworkSessionCoordinator.Instance.PublishCosmeticsAsync(JsonUtility.ToJson(snapshot.ToDto()), snapshot.Revision));
            _presenter.CloseCompleted += () => { closed++; _view.SetVisible(false); };
            _view.SetVisible(true); _presenter.SetActive(true);
            yield return null;
            var binding = _copy.GetComponent<ClosetViewBindings>();
            var preview = _copy.GetComponent<ClosetPreviewSurface>();
            Check(binding != null && binding.Validate(out _), "Production candidate references valid");
            Check(preview != null, "Private preview surface is connected");
            yield return Frames(3);
            Check(preview.VisualRoot != null && preview.PreviewCamera != null && preview.Texture != null,
                "Private render resources created: active=" + _copy.activeInHierarchy + ", surface=" + preview.isActiveAndEnabled
                + ", visual=" + (preview.VisualRoot != null) + ", camera=" + (preview.PreviewCamera != null) + ", texture=" + (preview.Texture != null));
            var sceneCameras = Camera.allCameras.Where(c => c != preview.PreviewCamera).ToArray();
            Check(sceneCameras.All(c => (c.cullingMask & (1 << preview.VisualRoot.layer)) == 0), "Scene cameras exclude preview layer");
            var cell = binding.Content.Find("Item_hat_01").GetComponent<ClosetItemCellBindings>();
            var originalCell = cell.GetInstanceID();
            long unchangedRevision = service.Snapshot.Revision;
            cell.Button.onClick.Invoke();
            Check(service.Snapshot.Revision == unchangedRevision && store.Writes == 0, "Selection alone changes neither equipment nor persistence");
            Check(binding.SelectedName.text.Contains(cell.Name.text), "Clicked card is the active draft");
            yield return Frames(3); yield return PreviewPixels(preview, "selected");
            Check(preview.VisualRoot.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == preview.VisualRoot.layer), "All dynamic layers isolated");
            binding.Equip.onClick.Invoke();
            Check(service.Snapshot.Head == "hat_01", "Explicit equip commits selected item");
            Check(binding.Content.Find("Item_hat_01").GetInstanceID() == cell.transform.GetInstanceID(), "Equip reuses item cell");
            yield return Capture("equipped");
            float zoom = preview.PreviewCamera.orthographicSize;
            foreach (var item in profile.Registry.GetByPart(CosmeticPart.Head))
            {
                binding.Content.Find("Item_" + item.Id).GetComponent<ClosetItemCellBindings>().Button.onClick.Invoke();
                yield return Frames(2);
                Check(service.Snapshot.Head == "hat_01", "Hat preview leaves equipment unchanged " + item.Id);
                Check(Mathf.Approximately(zoom, preview.PreviewCamera.orthographicSize), "Stable framing " + item.Id);
                CheckFraming(preview, item.Id);
                yield return PreviewPixels(preview, item.Id);
                binding.Equip.onClick.Invoke();
                Check(service.Snapshot.Head == item.Id, "Explicit hat equip " + item.Id);
                binding.Content.Find("Item_hat_01").GetComponent<ClosetItemCellBindings>().Button.onClick.Invoke();
                binding.Equip.onClick.Invoke();
                Check(service.Snapshot.Head == "hat_01", "Restore committed hat after " + item.Id);
            }
            binding.ResetPreview.onClick.Invoke();
            binding.Tabs[(int)CosmeticPart.Top].onClick.Invoke(); yield return Frames(2);
            foreach (var top in profile.Registry.GetByPart(CosmeticPart.Top).Where(i => i.Id.StartsWith("top_0", StringComparison.Ordinal)))
            {
                var before = service.Snapshot;
                binding.Content.Find("Item_" + top.Id).GetComponent<ClosetItemCellBindings>().Button.onClick.Invoke();
                Check(service.Snapshot.Revision == before.Revision, "Top preview does not equip " + top.Id);
                yield return Frames(3); yield return PreviewPixels(preview, top.Id);
                binding.Equip.onClick.Invoke();
                Check(service.Snapshot.Top == top.Id && service.Snapshot.Head == before.Head, "Top equip preserves hat " + top.Id);
                Check(service.Save().Success, "Top saved to isolated fixture store " + top.Id);
                var reload = new CosmeticEquipmentService(new CosmeticEquipState(), profile.Registry, store);
                Check(reload.Load().Success && reload.Snapshot.Top == top.Id, "Top reload from persisted store " + top.Id);
                binding.Unequip.onClick.Invoke();
                Check(service.Snapshot.Top == "", "Top unequip " + top.Id);
            }
            foreach (CosmeticPart part in Enum.GetValues(typeof(CosmeticPart)))
            {
                binding.Tabs[(int)part].onClick.Invoke(); yield return Frames(2);
                Check(_view.CurrentTab == part, "Tab selected " + part);
                var item = profile.Registry.GetByPart(part).FirstOrDefault();
                if (item == null) continue;
                string headBefore = service.Snapshot.Head;
                binding.Content.Find("Item_" + item.Id).GetComponent<ClosetItemCellBindings>().Button.onClick.Invoke();
                binding.Equip.onClick.Invoke();
                Check(service.Snapshot.Get(part) == item.Id, "Explicit equip " + part);
                if (part != CosmeticPart.Head) Check(service.Snapshot.Head == headBefore, "Other part preserved " + part);
                yield return Frames(2); yield return PreviewPixels(preview, "part-" + part);
                binding.Unequip.onClick.Invoke();
                Check(service.Snapshot.Get(part) == "", "Explicit unequip " + part);
            }
            binding.Tabs[0].onClick.Invoke();
            binding.Content.Find("Item_hat_01").GetComponent<ClosetItemCellBindings>().Button.onClick.Invoke();
            binding.Equip.onClick.Invoke();
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(800, 600) })
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed); yield return Frames(15);
                Check(preview.Texture != null && preview.Texture.IsCreated() && binding.Preview.texture == preview.Texture,
                    "Live texture after resize " + size);
                yield return Capture("size-" + size.x + "x" + size.y);
            }
            store.Fail = true; binding.Close.onClick.Invoke();
            Check(closed == 0 && _copy.activeSelf && binding.SaveStatus.text.Contains("저장 실패"), "Store failure retains screen and retry feedback");
            Check(service.Snapshot.Head == "hat_01", "Save failure preserves committed outfit");
            store.Fail = false;
            int cellsAfterAllTabs = binding.Content.childCount;
            for (int i = 0; i < 20; i++)
            {
                int writes = store.Writes;
                binding.Close.onClick.Invoke(); binding.Close.onClick.Invoke();
                Check(closed == i + 1 && store.Writes == writes + 1, "Single close/save event " + i);
                yield return Frames(2);
                Check(binding.Preview.texture == null && preview.PreviewCamera == null && preview.VisualRoot == null, "Preview resources detached " + i);
                Check(!Resources.FindObjectsOfTypeAll<RenderTexture>().Any(t => t.name == "ClosetPreview_Texture"), "Owned textures released " + i);
                Check(!Resources.FindObjectsOfTypeAll<Camera>().Any(c => c.name == "ClosetPreview_Camera"), "Owned cameras released " + i);
                _view.SetVisible(true); _presenter.SetActive(true); yield return Frames(2);
                Check(binding.Content.childCount == cellsAfterAllTabs && binding.Content.Find("Item_hat_01").GetComponent<ClosetItemCellBindings>().GetInstanceID() == originalCell,
                    "Keyed cells reused after reopen " + i);
            }
            _copy.SetActive(false); yield return Frames(2);
            Check(preview.VisualRoot == null, "Forced disable releases preview");
            _copy.SetActive(true); yield return Frames(2);
            Check(preview.VisualRoot != null, "Re-enable restores current committed preview");
            preview.Texture.Release(); yield return Frames(2);
            Check(preview.Texture.IsCreated() && binding.Preview.texture == preview.Texture, "Lost render target is replaced safely");
            var allocation = preview.AllocateTarget;
            preview.Release(); preview.AllocateTarget = (_, _) => null;
            preview.Show(service.Snapshot); yield return Frames(2);
            Check(preview.VisualRoot == null && preview.PreviewCamera == null && binding.Preview.texture == null
                && binding.PreviewHint.gameObject.activeSelf, "Injected allocation failure releases resources and shows feedback");
            preview.AllocateTarget = allocation; preview.Show(service.Snapshot); yield return Frames(2);
            Check(preview.Texture != null && preview.Texture.IsCreated(), "Preview recovers after allocation failure");
            yield return ReplaceOwnedAtlas(profile.Registry, preview);
            _presenter.Dispose(); _view.Dispose(); yield return Frames(2);
            Check(binding.Content.childCount == 0 && preview.VisualRoot == null && binding.Preview.texture == null, "Dispose releases owned view resources");
            Check(cameraMasks.All(p => p.Key == null || p.Key.cullingMask == p.Value), "All unmodified scene camera masks restored");
            // Actual scene unload, with a new private screen/owner and no save command.
            _view = new ClosetView(_copy);
            _presenter = new ClosetPresenter(_view, () => service, null);
            _presenter.SetActive(true); yield return Frames(2);
            Check(preview.Texture != null, "Preview alive before scene unload");
            var privateScene = UnityEngine.SceneManagement.SceneManager.CreateScene("ClosetLifetimeFixture");
            _copy.transform.SetParent(null);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_copy, privateScene);
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(privateScene);
            yield return Frames(2);
            Check(!Resources.FindObjectsOfTypeAll<RenderTexture>().Any(t => t.name == "ClosetPreview_Texture")
                && !Resources.FindObjectsOfTypeAll<Camera>().Any(c => c.name == "ClosetPreview_Camera"), "Actual scene unload releases private resources");
            Check(cameraMasks.All(p => p.Key == null || p.Key.cullingMask == p.Value), "Scene unload restores unrelated camera masks");
            Check(PlayerPrefs.HasKey(PlayerPrefsCosmeticStore.Key) == _hadSave
                && PlayerPrefs.GetString(PlayerPrefsCosmeticStore.Key, "") == _originalSave, "User equipment persistence untouched");
            Check(!new UnityServicesGateway().IsInitialized, "Offline Closet does not initialize UGS");
        }
        sealed class FaultStore : ICosmeticStore
        {
            readonly ICosmeticStore _inner;
            internal bool Fail; internal int Writes;
            internal FaultStore(ICosmeticStore inner) => _inner = inner;
            public string Read() => _inner.Read();
            public void Write(string json) { Writes++; if (Fail) throw new IOException("Injected store failure"); _inner.Write(json); }
        }
        static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        IEnumerator ReplaceOwnedAtlas(CosmeticRegistrySO registry, ClosetPreviewSurface preview)
        {
            // Private fault/authoring fixture objects only. Never edit catalog assets or
            // runtime game configuration; the private renderer gets a separate catalog.
            var ownedRegistry = Instantiate(registry);
            var source = registry.GetById("hat_01");
            var item = Instantiate(source); var atlas = Instantiate(source.Atlas);
            var originalSprite = source.Atlas.Bindings.First(b => b.Replacement != null).Replacement;
            var replacement = registry.GetById("hat_02").Atlas.Bindings.First(b => b.Replacement != null).Replacement;
            try
            {
                foreach (var binding in atlas.Bindings) if (binding.Replacement == originalSprite) binding.Replacement = replacement;
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(CosmeticItemSO).GetField("_atlas", flags).SetValue(item, atlas);
                var items = (List<CosmeticItemSO>)typeof(CosmeticRegistrySO).GetField("_allItems", flags).GetValue(ownedRegistry);
                items[items.FindIndex(i => i == source)] = item;
                typeof(CosmeticRegistrySO).GetMethod("RebuildLookups", flags).Invoke(ownedRegistry, null);
                _copy.SetActive(false); yield return Frames(2);
                preview.Registry = ownedRegistry;
                _copy.SetActive(true); yield return Frames(3);
                Check(preview.VisualRoot.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled && r.sprite == replacement),
                    "Reopened preview uses replacement mapping without UI/gameplay code change");
                Check(source.Atlas.Bindings.First(b => b.Replacement != null).Replacement == originalSprite
                    && registry.GetById("hat_01") == source, "Authoring fixture preserves shared catalog and atlas");
                yield return PreviewPixels(preview, "owned-atlas-replacement");
            }
            finally
            {
                _copy.SetActive(false); preview.Registry = registry;
                Destroy(ownedRegistry); Destroy(item); Destroy(atlas);
                _copy.SetActive(true);
            }
            yield return Frames(2);
        }
        void CheckFraming(ClosetPreviewSurface preview, string label)
        {
            foreach (var renderer in preview.VisualRoot.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!renderer.enabled || renderer.sprite == null) continue;
                var low = preview.PreviewCamera.WorldToViewportPoint(renderer.bounds.min);
                var high = preview.PreviewCamera.WorldToViewportPoint(renderer.bounds.max);
                Check(low.x >= 0 && low.y >= 0 && high.x <= 1 && high.y <= 1, "Supplied sprite inside frame " + label + "/" + renderer.name);
            }
        }
        IEnumerator PreviewPixels(ClosetPreviewSurface preview, string label)
        {
            yield return new WaitForEndOfFrame();
            var old = RenderTexture.active;
            var texture = new Texture2D(preview.Texture.width, preview.Texture.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = preview.Texture;
                texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32(); var bg = pixels[0]; int changed = 0;
                foreach (var p in pixels) if (Math.Abs(p.r - bg.r) + Math.Abs(p.g - bg.g) + Math.Abs(p.b - bg.b) > 35) changed++;
                Check(changed > pixels.Length / 20, "Actual character pixels in private target " + label);
                File.WriteAllBytes(Path.Combine(_directory, _mode + "-preview-" + label + ".png"), texture.EncodeToPNG());
            }
            finally { RenderTexture.active = old; Destroy(texture); }
        }
        IEnumerator Capture(string label)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Check(texture != null && texture.width > 100 && texture.height > 100, "Rendered capture " + label);
                File.WriteAllBytes(Path.Combine(_directory, _mode + "-" + label + ".png"), texture.EncodeToPNG());
            }
            finally { if (texture != null) Destroy(texture); }
        }
    }
}
#endif
