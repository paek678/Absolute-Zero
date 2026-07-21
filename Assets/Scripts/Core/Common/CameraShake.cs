using System.Collections;
using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        Vector3 _originalPos;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        void OnEnable()
        {
            _originalPos = transform.localPosition;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Shake(float duration, float magnitude)
        {
            StopAllCoroutines();
            StartCoroutine(ShakeRoutine(duration, magnitude));
        }

        IEnumerator ShakeRoutine(float duration, float magnitude)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;
                transform.localPosition = new Vector3(_originalPos.x + x, _originalPos.y + y, _originalPos.z);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.localPosition = _originalPos;
        }
    }
}
