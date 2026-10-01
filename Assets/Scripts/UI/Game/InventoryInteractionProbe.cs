#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Inventory;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using AbsoluteZero.UI.Game.Bridge;
using AbsoluteZero.UI.MiniGame;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AbsoluteZero.UI.Game
{
    // Explicit --az-matrix input only. InputSystem events exercise product input;
    // failed mini-game completion is injected, not claimed as human gameplay.
    public sealed class InventoryInteractionProbe : MonoBehaviour
    {
        GameUIManager _ui; InventoryPresenter _inventory; ILocalPlayerCommands _commands;
        Mouse _mouse; Keyboard _keyboard; int _checks; Vector2 _pointer;
        bool _multi;
        InputSettings _previousSettings, _probeSettings;
        readonly List<InputDevice> _physicalInputs = new();
        public void Initialize(GameUIManager ui, InventoryPresenter inventory, ILocalPlayerCommands commands)
        { _ui = ui; _inventory = inventory; _commands = commands; StartCoroutine(Run()); }

        IEnumerator Run()
        {
            // AddDevice disables new devices in an unfocused player unless IgnoreFocus
            // is already set. A later setting change does not re-enable those devices.
            // Clone the settings so this explicit test never edits the project asset.
            _previousSettings = InputSystem.settings;
            _probeSettings = Instantiate(_previousSettings);
            _probeSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = _probeSettings;
            foreach (var device in InputSystem.devices)
                if (device.enabled && (device is Mouse || device is Keyboard)) _physicalInputs.Add(device);
            foreach (var device in _physicalInputs) InputSystem.DisableDevice(device);
            _mouse = InputSystem.AddDevice<Mouse>(); _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse.MakeCurrent(); _keyboard.MakeCurrent();
            Check(_mouse.enabled && _keyboard.enabled, "probe input enabled before events");
            yield return Until(() => _inventory.LocalSlotCount == 4 && _inventory.GetLocalItemData(0)?.ItemName == "Hand Fan"
                && _inventory.GetLocalView(3) != null && IceboxController.Instance?.IsAnimating != true, "fixture visible");
            _multi = MatchCompositionRoot.Instance.ActiveConfig.Mode == GameMode.Multi;
            var local = _inventory.GetLocalPlayer();
            Check(_inventory.GetLocalSlot(0).CopyId != _inventory.GetLocalSlot(1).CopyId, "duplicate types have distinct identities");
            var first = _inventory.GetLocalView(0); var second = _inventory.GetLocalView(1);
            _inventory.SendMessage("RebuildLocalViews");
            yield return null;
            Check(first == _inventory.GetLocalView(0) && second == _inventory.GetLocalView(1), "rebuild retains copy objects");
            long full = _inventory.DebugMeasureRebuildAllocations(true, 12);
            int fullCreates = _inventory.DebugRebuildCreates;
            yield return Frames(3);
            long keyed = _inventory.DebugMeasureRebuildAllocations(false, 12);
            yield return Frames(3);
            Debug.Log($"[INPUT] ALLOCATION localRebuilds=12 full={full} keyed={keyed} fullCreates={fullCreates} keyedCreates={_inventory.DebugRebuildCreates} counterAvailable={full > 0}");
            Check(fullCreates == 48 && _inventory.DebugRebuildCreates == 0, "keyed refresh avoids 48 item object recreations");
            if (full > 0) Check(keyed < full, "keyed refresh reduces managed rebuild allocations");

            if (_multi)
            {
                yield return ClickItem(0);
                Check(_inventory.SelectionStage == ItemSelectionStage.Aiming, "click enters aim");
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape)); yield return Frames(2);
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); yield return Frames(2);
                Check(_inventory.SelectionStage == ItemSelectionStage.Idle, "escape cancels aim");
                yield return ClickItem(0); yield return Click(MouseButton.Right);
                Check(_inventory.SelectionStage == ItemSelectionStage.Idle, "right button cancels aim");
                yield return ClickItem(0); yield return ClickItem(0);
                Check(_inventory.SelectionStage == ItemSelectionStage.Idle, "same item cancels aim");
                ulong token = _inventory.BeginItemSubmission(0);
                bool sent = _commands.TrySelectItemWithTarget(0, (byte)local.PlayerIndex, _inventory.GetLocalSlot(0).CopyId);
                _inventory.CompleteItemSubmission(token, sent);
                yield return Until(() => _inventory.SelectionStage == ItemSelectionStage.Idle, "server invalid-target rejection releases input");
            }

            yield return Choose(3);
            yield return Until(() => MiniGameHub.IsRunning, "mini-game started");
            uint cancelledCopy = _inventory.GetLocalSlot(3).CopyId;
            byte cancelledUses = _inventory.GetLocalSlot(3).RemainingUses;
            yield return Until(() => !MiniGameHub.IsRunning && _inventory.SelectionStage == ItemSelectionStage.Idle,
                "server cancellation closes mini-game and releases input");
            Check(_inventory.GetLocalSlot(3).CopyId == cancelledCopy
                && _inventory.GetLocalSlot(3).RemainingUses == cancelledUses, "server cancellation preserves item uses");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Choose(3);
            yield return Until(() => MiniGameHub.IsRunning, "same-copy retry starts a fresh mini-game");
            var hub = FindFirstObjectByType<MiniGameHub>();
            var active = (MiniGameUIBase)typeof(MiniGameHub).GetField("_active", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hub);
            active.ForceCancel();
            yield return Until(() => !MiniGameHub.IsRunning && _inventory.SelectionStage == ItemSelectionStage.Idle, "mini-game failure releases input after server response");
            yield return new WaitForSecondsRealtime(1.2f); // Existing result overlay must finish.

            yield return Choose(1);
            yield return Until(() => local.HasSelectedItem.Value && _inventory.SelectionStage == ItemSelectionStage.Confirmed, "server confirms physical duplicate copy");
            Check(_inventory.IsConfirmedSlot(1) && !_inventory.IsConfirmedSlot(0), "only chosen duplicate highlighted");
            yield return ClickItem(1);
            yield return Until(() => !local.HasSelectedItem.Value && _inventory.SelectionStage == ItemSelectionStage.Idle, "same confirmed item cancels on server");
            yield return Choose(0);
            yield return Until(() => local.HasSelectedItem.Value, "reselect after cancellation");
            _commands.PressReady();
            yield return Until(() => local.IsReady.Value || TurnManager.Instance.CurrentPhase.Value != TurnPhase.PrepPhase, "Ready accepted");
            Check(!_commands.TryCancelSelection(), "Ready rejects cancellation");
            Check(_inventory.BeginItemSubmission(1) == 0, "Ready or closed prep rejects new input");
            Debug.Log("[INPUT] PASS checks=" + _checks + " seat=" + local.PlayerIndex);
        }

        IEnumerator Choose(int slot)
        {
            yield return ClickItem(slot);
            if (!_multi) yield break;
            Check(_inventory.SelectionStage == ItemSelectionStage.Aiming, "target item aiming");
            var root = MatchCompositionRoot.Instance;
            Check(root != null && root.ViewBindings != null, "scene has required target view bindings");
            byte seat = (byte)((_inventory.GetLocalPlayer().PlayerIndex + 1) % root.ActiveConfig.RequiredPlayerCount);
            Transform target = null;
            yield return Until(() => (target = root.ViewBindings.GetTarget(seat)) != null, "registered target ready");
            var col = target.GetComponentInChildren<Collider>();
            Check(col != null, "registered target has a collider");
            _pointer = Camera.main.WorldToScreenPoint(col.bounds.center + Vector3.up * col.bounds.extents.y * .35f);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = _pointer }); yield return Frames(3);
            Check(_ui.DebugTargetHoverVisible, "target arrow visible");
            byte snapped = (byte)typeof(GameUIManager).GetField("_snappedTargetSeat", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_ui);
            Debug.Log($"[INPUT] SNAP expected={seat} actual={snapped} pointer={_pointer} actualPointer={Mouse.current?.position.ReadValue()} current={Mouse.current == _mouse} overUI={UnityEngine.EventSystems.EventSystem.current?.IsPointerOverGameObject()}");
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            var events = UnityEngine.EventSystems.EventSystem.current;
            events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events) { position = _pointer }, hits);
            foreach (var hit in hits) Debug.Log($"[INPUT] POINTER_HIT {hit.gameObject.name} {hit.module.GetType().Name}");
            Check(snapped == seat, "arrow snapped to registered seat");
            Capture("aim-slot" + slot);
            yield return Frames(2);
            yield return Click(MouseButton.Left);
        }

        IEnumerator ClickItem(int slot)
        {
            yield return Until(() => IceboxController.Instance?.IsAnimating != true, "distribution settled before item click");
            var view = _inventory.GetLocalView(slot);
            Check(view != null, "item view exists " + slot);
            float end = Time.realtimeSinceStartup + 3f;
            do
            {
                view = _inventory.GetLocalView(slot);
                _pointer = Camera.main.WorldToScreenPoint(view.GetComponent<Collider>().bounds.center);
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = _pointer });
                yield return Frames(2);
            } while (HoverRaycaster.Instance?.CurrentHovered != view.Hover && Time.realtimeSinceStartup < end);
            if (HoverRaycaster.Instance?.CurrentHovered != view.Hover)
                Debug.Log($"[INPUT] RAYCAST_MISS slot={slot} pointer={_pointer} actual={Mouse.current?.position.ReadValue()} current={Mouse.current == _mouse} enabled={_mouse.enabled} focus={Application.isFocused} hit={HoverRaycaster.Instance?.CurrentHovered?.name} expected={view.name} pos={view.transform.position}");
            Check(HoverRaycaster.Instance?.CurrentHovered == view.Hover, "item raycast " + slot);
            yield return Click(MouseButton.Left);
        }
        IEnumerator Click(MouseButton button)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = _pointer }.WithButton(button));
            yield return Frames(2);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = _pointer }); yield return Frames(2);
        }
        static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        IEnumerator Until(Func<bool> predicate, string label)
        {
            float end = Time.realtimeSinceStartup + 15f;
            while (!predicate() && Time.realtimeSinceStartup < end) yield return null;
            Check(predicate(), label);
        }
        void Check(bool condition, string label)
        {
            if (!condition) { Debug.LogError("[INPUT] FAIL " + label); Application.Quit(2); throw new InvalidOperationException(label); }
            _checks++; Debug.Log("[INPUT] ASSERT " + label);
        }
        void Capture(string label) => ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(Application.consoleLogPath), "input-" + _inventory.GetLocalPlayer().PlayerIndex + "-" + label + ".png"));
        void OnDestroy()
        {
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            foreach (var device in _physicalInputs) if (device.added) InputSystem.EnableDevice(device);
            if (_probeSettings != null)
            {
                if (InputSystem.settings == _probeSettings) InputSystem.settings = _previousSettings;
                Destroy(_probeSettings);
            }
        }
    }
}
#endif
