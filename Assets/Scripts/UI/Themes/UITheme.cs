using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDownTacticalAI.UI.Components; // Added to resolve UIButtonScaler (it's in a different namespace)

namespace TopDownTacticalAI.UI.Themes
{
    /// <summary>
    /// UI Theme as a ScriptableObject - shared across all menus.
    /// Stores colors, fonts, button styles, and backgrounds so all UI screens look consistent.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "TopDownTacticalAI/UI Theme")]
    public class UITheme : ScriptableObject
    {
        /// <summary>Visual style of a button. Drives the colors used for the button's ColorBlock.</summary>
        public enum ButtonStyle
        {
            /// <summary>Standard neutral button (e.g. Resume, Settings, Restart, Main Menu, Cancel/No).</summary>
            Normal,
            /// <summary>Destructive / danger action (e.g. Quit, Confirm Quit "Yes"). Red-toned.</summary>
            Destructive
        }

        [Header("── Colors (Sci-Fi palette) ──")]
        // Sci-Fi: dark slate base with cyan/amber accent. Tuned so buttons look
        // like glowing HUD panels against a moody lab/space backdrop.
        public Color primaryColor = new Color(0.25f, 0.85f, 1f, 1f);     // Primary (cyan, "hologram")
        public Color secondaryColor = new Color(1f, 0.55f, 0.15f, 1f);    // Secondary (amber warning)
        public Color accentColor = new Color(0.65f, 1f, 0.85f, 1f);       // Accent (mint highlight)
        public Color backgroundColor = new Color(0.03f, 0.05f, 0.09f, 0.92f); // Deep space (dark, slight transparency)
        public Color panelColor = new Color(0.06f, 0.10f, 0.16f, 0.94f); // Slightly lighter panel
        public Color textColor = new Color(0.85f, 0.95f, 1f, 1f);         // Off-white with cyan tint
        public Color textDisabledColor = new Color(0.4f, 0.45f, 0.5f, 1f);
        public Color borderColor = new Color(0.25f, 0.6f, 0.85f, 0.65f); // Cyan border glow

        [Header("── Button States (Normal style) ──")]
        public Color buttonNormal = new Color(0.10f, 0.22f, 0.35f, 0.95f);       // Dim teal
        public Color buttonHighlighted = new Color(0.30f, 0.75f, 0.95f, 1f);     // Bright cyan glow
        public Color buttonPressed = new Color(0.08f, 0.18f, 0.28f, 1f);
        public Color buttonDisabled = new Color(0.15f, 0.18f, 0.22f, 0.5f);

        [Header("── Button States (Destructive style) ──")]
        [Tooltip("Normal color for destructive buttons (e.g. Quit, Yes-confirm). Sci-Fi danger red.")]
        public Color buttonDestructiveNormal = new Color(0.85f, 0.18f, 0.25f, 1f);
        [Tooltip("Hover color for destructive buttons.")]
        public Color buttonDestructiveHighlighted = new Color(1f, 0.35f, 0.40f, 1f);
        [Tooltip("Pressed color for destructive buttons.")]
        public Color buttonDestructivePressed = new Color(0.55f, 0.10f, 0.15f, 1f);

        [Header("── Fonts ──")]
        public TMP_FontAsset mainFont;
        public TMP_FontAsset headerFont;
        public int defaultFontSize = 28;
        public int headerFontSize = 48;
        public int buttonFontSize = 32;

        [Header("── Sprites (9-slice) ──")]
        [Tooltip("Normal button background (9-slice)")]
        public Sprite buttonBackground;
        [Tooltip("Hovered button background (9-slice)")]
        public Sprite buttonBackgroundHover;
        [Tooltip("Pressed button background (9-slice)")]
        public Sprite buttonBackgroundPressed;
        [Tooltip("Panel background (9-slice)")]
        public Sprite panelBackground;
        [Tooltip("Slider track background")]
        public Sprite sliderTrack;
        [Tooltip("Slider handle background")]
        public Sprite sliderHandle;
        [Tooltip("Toggle background")]
        public Sprite toggleBackground;
        [Tooltip("Sprite for Toggle checkmark")]
        public Sprite toggleCheckmark;

        [Header("── Animation ──")]
        public float fadeDuration = 0.25f;
        public float slideDuration = 0.3f;
        public float buttonPressScale = 0.95f;
        public float buttonHoverScale = 1.05f;
        public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("── Layout ──")]
        public float panelPadding = 30f;
        public float buttonSpacing = 15f;
        public float elementSpacing = 20f;
        public Vector2 defaultButtonSize = new Vector2(300, 70);
        public Vector2 largeButtonSize = new Vector2(400, 90);

        /// <summary>Applies the theme to a Button (TextMeshPro + Image) automatically using the Normal style.</summary>
        public void ApplyToButton(Button button, TMP_Text buttonText = null, Image buttonImage = null)
        {
            ApplyToButton(button, ButtonStyle.Normal, buttonText, buttonImage);
        }

        /// <summary>
        /// Applies the theme to a Button with a specific style. Also forces Transition = ColorTint and
        /// (when no 9-slice buttonBackground sprite is assigned) sets the Image type to Simple, so the
        /// button looks like a normal clickable button instead of getting a dropdown-like sliced artifact.
        /// </summary>
        public void ApplyToButton(Button button, ButtonStyle style, TMP_Text buttonText = null, Image buttonImage = null)
        {
            if (button == null) return;
            if (buttonImage == null) buttonImage = button.GetComponent<Image>();
            if (buttonText == null) buttonText = button.GetComponentInChildren<TMP_Text>();

            // Force ColorTint transition so hover/press feedback actually shows.
            // (If the button was using SpriteSwap with a missing/incompatible sprite, it would look
            // unresponsive on click — that's the "looks like a dropdown" symptom.)
            button.transition = Selectable.Transition.ColorTint;

            // Setup ColorBlock from the chosen style
            var colors = button.colors;
            switch (style)
            {
                case ButtonStyle.Destructive:
                    colors.normalColor = buttonDestructiveNormal;
                    colors.highlightedColor = buttonDestructiveHighlighted;
                    colors.pressedColor = buttonDestructivePressed;
                    break;
                case ButtonStyle.Normal:
                default:
                    colors.normalColor = buttonNormal;
                    colors.highlightedColor = buttonHighlighted;
                    colors.pressedColor = buttonPressed;
                    break;
            }
            colors.disabledColor = buttonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            // Setup Image background.
            // If we have a 9-slice buttonBackground sprite, use it sliced. Otherwise, render as Simple
            // (a flat colored rect) so the button doesn't show stretched/distorted sprite artifacts.
            if (buttonImage != null)
            {
                if (buttonBackground != null)
                {
                    buttonImage.sprite = buttonBackground;
                    buttonImage.type = Image.Type.Sliced;
                    buttonImage.color = Color.white;
                }
                else
                {
                    buttonImage.sprite = null;
                    buttonImage.type = Image.Type.Simple;
                    // For destructive style without a sprite, tint the background red so the danger is visible.
                    if (style == ButtonStyle.Destructive)
                        buttonImage.color = buttonDestructiveNormal;
                    else
                        buttonImage.color = buttonNormal;
                }
            }

            // Setup Text
            if (buttonText != null)
            {
                if (mainFont != null) buttonText.font = mainFont;
                buttonText.fontSize = buttonFontSize;
                buttonText.color = textColor;
                buttonText.alignment = TextAlignmentOptions.Center;
                buttonText.enableAutoSizing = true;
                buttonText.fontSizeMin = 20;
                buttonText.fontSizeMax = 40;
            }

            // Add hover/press scale animation
            var scaler = button.GetComponent<UIButtonScaler>();
            if (scaler == null) scaler = button.gameObject.AddComponent<UIButtonScaler>();
            scaler.Initialize(buttonHoverScale, buttonPressScale, scaleCurve);
        }

        /// <summary>Applies the theme to a Panel (Image background) automatically.</summary>
        public void ApplyToPanel(Image panelImage)
        {
            if (panelImage != null && panelBackground != null)
            {
                panelImage.sprite = panelBackground;
                panelImage.type = Image.Type.Sliced;
                panelImage.color = panelColor;
            }
        }

        /// <summary>Applies the theme to Text automatically.</summary>
        public void ApplyToText(TMP_Text text, bool isHeader = false)
        {
            if (text == null) return;
            if (isHeader && headerFont != null) text.font = headerFont;
            else if (mainFont != null) text.font = mainFont;
            text.fontSize = isHeader ? headerFontSize : defaultFontSize;
            text.color = textColor;
        }

        /// <summary>Applies the theme to Slider automatically.</summary>
        public void ApplyToSlider(Slider slider)
        {
            if (slider == null) return;

            var track = slider.transform.Find("Background")?.GetComponent<Image>();
            var handle = slider.transform.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
            var fill = slider.transform.Find("Fill Area/Fill")?.GetComponent<Image>();

            if (track != null && sliderTrack != null)
            {
                track.sprite = sliderTrack;
                track.type = Image.Type.Sliced;
                track.color = new Color(0.2f, 0.25f, 0.35f, 1f);
            }

            if (handle != null && sliderHandle != null)
            {
                handle.sprite = sliderHandle;
                handle.color = primaryColor;
            }

            if (fill != null)
            {
                fill.color = primaryColor;
                if (sliderTrack != null)
                {
                    fill.sprite = sliderTrack;
                    fill.type = Image.Type.Filled;
                    fill.fillMethod = Image.FillMethod.Horizontal;
                }
            }
        }

        /// <summary>Applies the theme to Toggle automatically.</summary>
        public void ApplyToToggle(Toggle toggle)
        {
            if (toggle == null) return;

            var bg = toggle.transform.Find("Background")?.GetComponent<Image>();
            var checkmark = toggle.transform.Find("Background/Checkmark")?.GetComponent<Image>();

            if (bg != null && toggleBackground != null)
            {
                bg.sprite = toggleBackground;
                bg.type = Image.Type.Sliced;
                bg.color = panelColor;
            }

            if (checkmark != null && toggleCheckmark != null)
            {
                checkmark.sprite = toggleCheckmark;
                checkmark.color = accentColor;
            }

            var label = toggle.GetComponentInChildren<TMP_Text>();
            if (label != null) ApplyToText(label);
        }

        /// <summary>Applies the theme to Dropdown automatically.</summary>
        public void ApplyToDropdown(TMP_Dropdown dropdown)
        {
            if (dropdown == null) return;

            var template = dropdown.template;
            if (template != null)
            {
                var bg = template.GetComponent<Image>();
                if (bg != null && panelBackground != null)
                {
                    bg.sprite = panelBackground;
                    bg.type = Image.Type.Sliced;
                    bg.color = panelColor;
                }

                var viewport = template.Find("Viewport")?.GetComponent<Image>();
                if (viewport != null && panelBackground != null)
                {
                    viewport.sprite = panelBackground;
                    viewport.type = Image.Type.Sliced;
                    viewport.color = panelColor;
                }

                var item = template.Find("Viewport/Content/Item")?.GetComponent<Toggle>();
                if (item != null) ApplyToToggle(item);
            }

            var label = dropdown.GetComponentInChildren<TMP_Text>();
            if (label != null) ApplyToText(label);

            var arrow = dropdown.transform.Find("Arrow")?.GetComponent<Image>();
            if (arrow != null) arrow.color = accentColor;
        }
    }
}
