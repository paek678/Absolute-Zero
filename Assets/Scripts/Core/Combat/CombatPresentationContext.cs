using System.Collections.Generic;
using AbsoluteZero.Core.Inventory;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Combat
{
    // Frozen identity and references, not another registry or gameplay state owner.
    internal sealed class CombatPresentationContext
    {
        readonly Dictionary<int, PlayerBinding> _bindings = new();
        readonly MatchCompositionRoot _match;
        readonly long _generation;
        readonly LocalMatchPerspective _perspective;
        public NetworkManager Network { get; }
        public PlayerBinding Human { get; }
        public FPSVisualController Fps { get; }
        public MatchViewBindings Views { get; }
        public InventoryPresenter Inventory { get; }
        public ItemManager Items { get; }
        public Camera Camera { get; }

        public CombatPresentationContext(GameObject owner)
        {
            _match = MatchCompositionRoot.Instance;
            _generation = _match != null ? _match.Generation : 0;
            Network = NetworkManager.Singleton;
            LocalMatchPerspective.TryResolveCurrent(out _perspective);
            Human = _perspective.HumanBinding;
            if (_match != null && _match.Registry != null)
                foreach (var binding in _match.Registry.Players)
                    if (binding.HasIdentity && binding.IsValid)
                        _bindings[binding.Identity.PlayerIndex] = binding;
            Views = MatchViewBindings.ForScene(owner.scene);
            Fps = FPSVisualController.Instance;
            Inventory = InventoryPresenter.Instance;
            Items = ItemManager.Instance;
            Camera = Views != null ? Views.GameplayCamera : UnityEngine.Camera.main;
        }

        public bool IsCurrent => _match != null && _match == MatchCompositionRoot.Instance
            && _match.Generation == _generation && _match.IsSessionCurrent
            && Network != null && Network == NetworkManager.Singleton
            && Network.IsConnectedClient && !Network.ShutdownInProgress
            && Human != null && IsBindingCurrent(Human);

        public bool IsBindingCurrent(PlayerBinding binding)
            => binding != null && binding.IsValid
                && _perspective.TryGetBinding(binding.Identity.PlayerIndex, out var current)
                && ReferenceEquals(binding, current);

        public PlayerBinding GetBinding(int seat)
            => _bindings.TryGetValue(seat, out var binding) && IsBindingCurrent(binding) ? binding : null;

        public AZPlayerVisual GetVisual(int seat)
        {
            var binding = GetBinding(seat);
            return binding != null ? binding.NetworkObject.GetComponent<AZPlayerVisual>() : null;
        }

        public Vector3 GetWorldPosition(int seat)
        {
            var binding = GetBinding(seat);
            var visual = GetVisual(seat);
            if (visual != null) return visual.GetVisualPosition();
            if (binding != null) return binding.NetworkObject.transform.position;
            // Compatibility fixture only; never reinterpret a replacement player's binding.
            var spawn = GameObject.Find($"SpawnPoint_{seat + 1}");
            return spawn != null ? spawn.transform.position + Vector3.up * 1.5f : Vector3.zero;
        }

        public IEnumerable<AZPlayerVisual> CurrentVisuals
        {
            get
            {
                foreach (var entry in _bindings)
                {
                    var visual = GetVisual(entry.Key);
                    if (visual != null) yield return visual;
                }
            }
        }

        // Packet arrays may be reused by a producer after receipt; queued work owns its copy.
        internal static CombatResolutionBatchNetData CopyBatch(CombatResolutionBatchNetData value)
        {
            value.Events = value.Events == null ? null : (CombatEventNetData[])value.Events.Clone();
            value.TempBefore = value.TempBefore == null ? null : (float[])value.TempBefore.Clone();
            value.TempAfter = value.TempAfter == null ? null : (float[])value.TempAfter.Clone();
            value.MainItemIds = value.MainItemIds == null ? null : (short[])value.MainItemIds.Clone();
            value.SubItemIds = value.SubItemIds == null ? null : (short[])value.SubItemIds.Clone();
            value.ActionOrder = value.ActionOrder == null ? null : (int[])value.ActionOrder.Clone();
            return value;
        }
    }
}
