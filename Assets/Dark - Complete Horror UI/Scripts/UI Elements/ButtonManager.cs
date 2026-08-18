using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

namespace Michsky.UI.Dark
{
    [RequireComponent(typeof(Button))]
    public class ButtonManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        // Content
        public string buttonText = "Button";
        public UnityEvent clickEvent;
        public UnityEvent hoverEvent;
        public UnityEvent onLeave;
        public AudioClip hoverSound;
        public AudioClip clickSound;
        public Button buttonVar;

        // Resources
        public TextMeshProUGUI normalText;
        public TextMeshProUGUI highlightedText;
        public AudioSource soundSource;
        public GameObject rippleParent;

        // Settings
        public bool useCustomContent = false;
        public bool enableButtonSounds = false;
        public bool useHoverSound = true;
        public bool useClickSound = true;
        public bool useRipple = true;
        public bool fixWidth = false;

        // Ripple
        public RippleUpdateMode rippleUpdateMode = RippleUpdateMode.UNSCALED_TIME;
        public Sprite rippleShape;
        [Range(0.1f, 5)] public float speed = 1f;
        [Range(0.5f, 25)] public float maxSize = 4f;
        public Color startColor = new Color(1f, 1f, 1f, 1f);
        public Color transitionColor = new Color(1f, 1f, 1f, 1f);
        public bool renderOnTop = false;
        public bool centered = false;

        // Helpers
        float cachedStateLength = 0.5f;
        Animator animator;
        bool isPointerOn;

        public enum RippleUpdateMode
        {
            NORMAL,
            UNSCALED_TIME
        }

        void Start()
        {
            if (buttonVar == null) { buttonVar = gameObject.GetComponent<Button>(); }
            if (enableButtonSounds && useClickSound && soundSource != null) { buttonVar.onClick.AddListener(delegate { soundSource.PlayOneShot(clickSound); }); }

            buttonVar.onClick.AddListener(delegate { clickEvent.Invoke(); });

            if (useRipple && rippleParent != null) { rippleParent.SetActive(false); }
            else if (!useRipple && rippleParent != null) { Destroy(rippleParent); }

            if (gameObject.GetComponent<Animator>() != null)
            {
                animator = gameObject.GetComponent<Animator>();
                cachedStateLength = DarkInternalTools.GetAnimatorClipLength(animator, "Highlighted") + 0.1f;
            }
        }

        void OnEnable()
        {
            if (!useCustomContent) 
            {
                UpdateUI(); 
            }
        }

        public void UpdateUI()
        {
            normalText.text = buttonText;
            highlightedText.text = buttonText;

            if (fixWidth) { StartCoroutine("FixWidthHelper"); }
        }

        public void UpdateUIEditor()
        {
	   if (useCustomContent)
		return;
            
            normalText.text = buttonText;
            highlightedText.text = buttonText;
        }

        public void CreateRipple(Vector2 pos)
        {
            if (rippleParent != null)
            {
                GameObject rippleObj = new GameObject();
                rippleObj.AddComponent<Image>();
                rippleObj.GetComponent<Image>().sprite = rippleShape;
                rippleObj.name = "Ripple";
                rippleParent.SetActive(true);
                rippleObj.transform.SetParent(rippleParent.transform);

                if (renderOnTop) { rippleParent.transform.SetAsLastSibling(); }
                else { rippleParent.transform.SetAsFirstSibling(); }

                if (centered) { rippleObj.transform.localPosition = new Vector2(0f, 0f); }
                else { rippleObj.transform.position = pos; }

                rippleObj.AddComponent<Ripple>();
                Ripple tempRipple = rippleObj.GetComponent<Ripple>();
                tempRipple.speed = speed;
                tempRipple.maxSize = maxSize;
                tempRipple.startColor = startColor;
                tempRipple.transitionColor = transitionColor;

                if (rippleUpdateMode == RippleUpdateMode.NORMAL) { tempRipple.unscaledTime = false; }
                else { tempRipple.unscaledTime = true; }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (useRipple && isPointerOn)
            {
                CreateRipple(Mouse.current.position.ReadValue());
            }

            // If this is the "Continue" chapter button, prefer showing the Modal Window prefab in the editor
            // instead of immediately firing other click handlers that may load a scene. This avoids jumping
            // straight into a level when testing in the Editor while preserving existing wiring elsewhere.

            // Some input setups or overlaying UI elements can prevent the Button.onClick event from firing on release.
            // Invoke the configured clickEvent on pointer down as a fallback so the button responds immediately to clicks.
            // Guard with interactable check to avoid invoking for disabled buttons.
            if (buttonVar != null && buttonVar.interactable)
            {
                // Play click sound if configured (ensure it doesn't double-play if also wired via buttonVar.onClick)
                if (enableButtonSounds && useClickSound && soundSource != null)
                {
                    soundSource.PlayOneShot(clickSound);
                }

                // Invoke the click event safely.
                clickEvent?.Invoke();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (enableButtonSounds && useHoverSound && buttonVar.interactable && soundSource != null)
                soundSource.PlayOneShot(hoverSound);

            hoverEvent.Invoke();
            isPointerOn = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerOn = false;
            onLeave.Invoke();
        }

        IEnumerator FixWidthHelper()
        {
            yield return new WaitForSeconds(0.1f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(gameObject.GetComponent<RectTransform>());
            StopCoroutine("FixWidthHelper");
        }

        IEnumerator DisableAnimator()
        {
            yield return new WaitForSecondsRealtime(cachedStateLength);
            animator.enabled = false;
        }
    }
}