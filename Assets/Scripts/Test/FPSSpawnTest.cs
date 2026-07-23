using System.Collections;
using UnityEngine;

namespace AbsoluteZero.Test
{
    public class FPSSpawnTest : MonoBehaviour
    {
        [Header("Test Settings")]
        public float AnimPlayTime = 1.5f;
        public float DelayBetween = 1.5f;

        static readonly string[] Triggers =
        {
            "gun", "swing", "tape", "mask", "hug",
            "card", "defence", "eat", "fan", "use", "feed"
        };

        Animator _animator;
        WaitForSeconds _waitAnim;
        WaitForSeconds _waitDelay;

        void Start()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null) return;

            _waitAnim = new WaitForSeconds(AnimPlayTime);
            _waitDelay = new WaitForSeconds(DelayBetween);
            StartCoroutine(CycleAnimations());
        }

        IEnumerator CycleAnimations()
        {
            int idx = 0;
            while (true)
            {
                _animator.SetTrigger(Triggers[idx]);
                yield return _waitAnim;
                _animator.SetTrigger("end");
                yield return _waitDelay;
                idx = (idx + 1) % Triggers.Length;
            }
        }
    }
}
