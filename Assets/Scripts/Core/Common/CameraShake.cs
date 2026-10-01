using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        OwnedPositionOffset _offset;
        LocalSettingsService _settings;
        readonly System.Random _random = new();
        float _remaining, _magnitude;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        void OnEnable()
        {
            _offset ??= new OwnedPositionOffset(() => transform.localPosition, value => transform.localPosition = value);
            _settings = LocalSettingsRuntime.Instance?.Service;
            if (_settings != null) _settings.Changed += OnSettingsChanged;
        }

        void OnDisable()
        {
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
            _remaining = 0; _offset?.Clear();
        }
        void OnSettingsChanged() { if (!_settings.Current.Shake) { _remaining = 0; _offset?.Clear(); } }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Shake(float duration, float magnitude)
        {
            if (!isActiveAndEnabled || (_settings != null && !_settings.Current.Shake)) return;
            _remaining = Mathf.Max(0, duration); _magnitude = Mathf.Max(0, magnitude);
        }

        void LateUpdate()
        {
            if (_remaining > 0)
            {
                _offset.Apply(new Vector3((float)(_random.NextDouble() * 2 - 1) * _magnitude,
                    (float)(_random.NextDouble() * 2 - 1) * _magnitude, 0));
                _remaining -= Time.deltaTime;
            }
            else _offset?.Clear();
        }
    }
}
