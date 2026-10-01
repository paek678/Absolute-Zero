using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Collections;
using UnityEngine;

namespace AbsoluteZero.Core.Cosmetic
{
    public class CosmeticProfileService : MonoBehaviour
    {
        const string NicknameKey = "player_nickname";
        static readonly Regex NicknameRegex = new(@"^[가-힣a-zA-Z0-9]+$", RegexOptions.Compiled);

        [SerializeField] CosmeticRegistrySO _registry;

        public static CosmeticProfileService Instance { get; private set; }

        string _nickname;
        CosmeticEquipState _equipState;

        public string Nickname => _nickname;
        public CosmeticEquipState EquipState => _equipState;
        public CosmeticRegistrySO Registry => _registry;
        public CosmeticEquipmentService Equipment { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _nickname = PlayerPrefs.GetString(NicknameKey, "");

            _equipState = new CosmeticEquipState();
            Equipment = new CosmeticEquipmentService(_equipState, _registry, new PlayerPrefsCosmeticStore());
            if (_registry != null)
            {
                var loaded = Equipment.Load();
                if (!loaded.Success) Debug.LogWarning("[CosmeticProfileService] Load failed: " + loaded.Error);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool TryValidateNickname(string raw, out string validated)
        {
            validated = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string trimmed = raw.Trim().Normalize(NormalizationForm.FormC);
            if (!NicknameRegex.IsMatch(trimmed)) return false;

            int textLen = new StringInfo(trimmed).LengthInTextElements;
            if (textLen < 2 || textLen > 8) return false;

            validated = trimmed;
            return true;
        }

        public void SetNickname(string validated)
        {
            _nickname = validated;
            PlayerPrefs.SetString(NicknameKey, validated);
            PlayerPrefs.Save();
        }

        public string GetCompactDto()
        {
            var dto = _equipState.ToDto();
            string json = JsonUtility.ToJson(dto);

            if (Encoding.UTF8.GetByteCount(json) > 125)
            {
                Debug.LogWarning("[CosmeticProfileService] Compact DTO exceeds 125 UTF-8 bytes — returning empty");
                return "";
            }

            return json;
        }

        public bool TryValidateAndCanonicalizeDto(string raw, out FixedString128Bytes canonical)
        {
            canonical = default;
            if (!CosmeticCodec.TryParse(raw, out var dto) || !CosmeticCodec.TryEncode(dto, _registry, out var json))
                return false;
            canonical = new FixedString128Bytes(json);
            return true;
        }
    }
}
