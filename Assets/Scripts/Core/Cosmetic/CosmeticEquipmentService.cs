using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Cosmetic
{
    public readonly struct CosmeticOperationResult
    {
        public bool Success { get; }
        public long Revision { get; }
        public string Error { get; }
        public CosmeticOperationResult(bool success, long revision, string error = null)
        { Success = success; Revision = revision; Error = error; }
    }

    public interface ICosmeticStore
    {
        string Read();
        void Write(string json);
    }

    public sealed class PlayerPrefsCosmeticStore : ICosmeticStore
    {
        public const string Key = "cosmetic_equip_v1";
        readonly string _key;
        public PlayerPrefsCosmeticStore(string key = Key)
            => _key = !string.IsNullOrEmpty(key) ? key : throw new ArgumentException(nameof(key));
        public string Read() => PlayerPrefs.GetString(_key, "");
        public void Write(string json)
        {
            PlayerPrefs.SetString(_key, json);
            PlayerPrefs.Save();
        }
    }

    /// <summary>App-owned commands over the existing state; SO catalog remains read-only.</summary>
    public sealed class CosmeticEquipmentService
    {
        readonly CosmeticEquipState _state;
        readonly CosmeticRegistrySO _registry;
        readonly ICosmeticStore _store;
        public long SavedRevision { get; private set; } = -1;
        public CosmeticSnapshot Snapshot => new(_state.ToDto(), _state.Revision);
        public CosmeticEquipmentService(CosmeticEquipState state, CosmeticRegistrySO registry, ICosmeticStore store)
        { _state = state; _registry = registry; _store = store; }
        public IReadOnlyList<CosmeticItemSO> GetItems(CosmeticPart part)
            => _registry != null ? _registry.GetByPart(part).AsReadOnly() : Array.Empty<CosmeticItemSO>();

        public CosmeticOperationResult Set(CosmeticPart part, string id)
        {
            var candidate = _state.ToDto();
            if (!CosmeticCodec.SetPart(candidate, part, id ?? "")) return Failure("지원하지 않는 부위입니다.");
            return Apply(candidate);
        }
        public CosmeticOperationResult Apply(CosmeticDto candidate)
        {
            if (!CosmeticCodec.TryEncode(candidate, _registry, out _)) return Failure("장비 정보를 확인해 주세요.");
            _state.ApplyValidated(candidate, _registry);
            return new(true, _state.Revision);
        }
        public CosmeticOperationResult Save()
        {
            var snapshot = Snapshot;
            if (!CosmeticCodec.TryEncode(snapshot.ToDto(), _registry, out var json)) return Failure("장비 정보를 저장할 수 없습니다.");
            try
            {
                _store.Write(json);
                SavedRevision = snapshot.Revision;
                return new(true, snapshot.Revision);
            }
            catch (Exception error) { return new(false, snapshot.Revision, error.Message); }
        }
        public CosmeticOperationResult Load()
        {
            try
            {
                var raw = _store.Read();
                // Preserve legacy fallback: malformed saves reset; unknown individual IDs are skipped.
                var recovered = new CosmeticEquipState();
                recovered.FromDto(CosmeticCodec.TryParse(raw, out var dto) ? dto : null, _registry);
                _state.ApplyValidated(recovered.ToDto(), _registry);
                SavedRevision = -1; // A recovered/fallback save has not been written by this instance.
                return new(true, _state.Revision);
            }
            catch (Exception error) { return Failure(error.Message); }
        }
        CosmeticOperationResult Failure(string reason) => new(false, _state.Revision, reason);
    }
}
