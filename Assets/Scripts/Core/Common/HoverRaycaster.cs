using UnityEngine;
using UnityEngine.InputSystem;

namespace AbsoluteZero.Core.Common
{
    public class HoverRaycaster : MonoBehaviour
    {
        public static HoverRaycaster Instance { get; private set; }

        Camera _cam;
        HoverEffect _currentHovered;
        int _interactableMask;

        public HoverEffect CurrentHovered => _currentHovered;

        void Awake()
        {
            Instance = this;
            _interactableMask = LayerMask.GetMask("Interactable");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Mouse.current == null) return;

            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = _cam.ScreenPointToRay(mousePos);
            HoverEffect hit = null;

            if (Physics.Raycast(ray, out RaycastHit hitInfo, 100f, _interactableMask))
                hit = hitInfo.collider.GetComponent<HoverEffect>();

            if (hit == _currentHovered) return;

            if (_currentHovered != null)
                _currentHovered.SetHovered(false);

            _currentHovered = hit;

            if (_currentHovered != null)
                _currentHovered.SetHovered(true);
        }
    }
}
