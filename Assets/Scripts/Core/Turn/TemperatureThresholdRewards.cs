using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Turn
{
    // Match-scoped server observer. Rewards do not depend on the Prep tick timer
    // or on how quickly either human/bot submits Ready.
    internal sealed class TemperatureThresholdRewards : IDisposable
    {
        readonly List<Binding> _bindings = new();
        readonly TemperatureSystem _temperature;
        readonly ItemDropTable _drops;
        readonly bool _multi;
        readonly int _maxRandom;
        readonly Func<bool> _canGrant;
        bool _disposed;

        internal TemperatureThresholdRewards(PlayerState[] players, TemperatureSystem temperature,
            ItemDropTable drops, bool multi, int maxRandom, Func<bool> canGrant)
        {
            _temperature = temperature ?? throw new ArgumentNullException(nameof(temperature));
            _drops = drops;
            _multi = multi;
            _maxRandom = maxRandom;
            _canGrant = canGrant ?? throw new ArgumentNullException(nameof(canGrant));
            foreach (var player in players)
                if (player != null) _bindings.Add(new Binding(this, player));
        }

        void OnDecrease(PlayerState player, float before, float after)
        {
            if (_disposed || player == null || after >= before || after <= TemperatureSystem.MIN_TEMP
                || player.CurrentLifeState.Value != LifeState.Alive || !_canGrant()) return;
            var inventory = player.GetInventory();
            if (inventory == null || !inventory.IsServer) return;
            _temperature.CheckThresholds(player, inventory, inventory.GetThresholdGranted(),
                _drops, _multi, _maxRandom);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var binding in _bindings) binding.Dispose();
            _bindings.Clear();
        }

        sealed class Binding : IDisposable
        {
            readonly TemperatureThresholdRewards _owner;
            readonly PlayerState _player;
            internal Binding(TemperatureThresholdRewards owner, PlayerState player)
            {
                _owner = owner;
                _player = player;
                player.Temperature.OnValueChanged += Changed;
            }
            void Changed(float before, float after) => _owner.OnDecrease(_player, before, after);
            public void Dispose()
            {
                if (_player != null) _player.Temperature.OnValueChanged -= Changed;
            }
        }
    }
}
