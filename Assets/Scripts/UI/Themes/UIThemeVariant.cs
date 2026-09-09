using UnityEngine;

namespace TopDownTacticalAI.UI.Themes
{
    /// <summary>
    /// Predefined theme variants matching the mockup screens (Day / Night).
    /// Use <see cref="BuildInto"/> to apply the variant onto a UITheme asset.
    /// </summary>
    [System.Serializable]
    public class UIThemeVariant
    {
        public enum Mode
        {
            Day = 0,
            Night = 1
        }

        [Header("Variant Identity")]
        public string variantName = "Day";
        public Mode mode = Mode.Day;

        [Header("Primary Palette")]
        [Tooltip("Primary brand color (titles, selected outline, START button)")]
        public Color primaryColor = new Color(0.2f, 0.6f, 1f, 1f);
        [Tooltip("Secondary color (easy difficulty, etc.)")]
        public Color secondaryColor = new Color(0.9f, 0.3f, 0.2f, 1f);
        [Tooltip("Accent (stars, gold)")]
        public Color accentColor = new Color(1f, 0.85f, 0.2f, 1f);
        [Tooltip("Background color (panel base)")]
        public Color backgroundColor = new Color(0.95f, 0.95f, 0.98f, 1f);
        [Tooltip("Panel color (card surface)")]
        public Color panelColor = new Color(1f, 1f, 1f, 1f);
        [Tooltip("Primary text color")]
        public Color textColor = new Color(0.1f, 0.1f, 0.15f, 1f);
        [Tooltip("Disabled/secondary text color")]
        public Color textDisabledColor = new Color(0.55f, 0.55f, 0.6f, 1f);
        [Tooltip("Border/divider color")]
        public Color borderColor = new Color(0.85f, 0.85f, 0.9f, 1f);

        [Header("Button States")]
        public Color buttonNormal = new Color(0.9f, 0.92f, 0.96f, 1f);
        public Color buttonHighlighted = new Color(0.75f, 0.85f, 1f, 1f);
        public Color buttonPressed = new Color(0.7f, 0.8f, 0.95f, 1f);
        public Color buttonDisabled = new Color(0.9f, 0.9f, 0.9f, 0.6f);

        /// <summary>
        /// Returns the built-in Day (light) variant matching the white mockup.
        /// </summary>
        public static UIThemeVariant CreateDay()
        {
            return new UIThemeVariant
            {
                variantName = "Day",
                mode = Mode.Day,
                primaryColor = new Color(0.95f, 0.2f, 0.45f, 1f),    // Pink/Red (BADGE-like, used in mockup outline)
                secondaryColor = new Color(0.95f, 0.75f, 0.1f, 1f),   // Orange
                accentColor = new Color(0.95f, 0.55f, 0.1f, 1f),      // Orange accent
                backgroundColor = new Color(0.98f, 0.97f, 0.99f, 1f), // Soft white
                panelColor = new Color(1f, 1f, 1f, 1f),               // Pure white card
                textColor = new Color(0.1f, 0.1f, 0.2f, 1f),          // Near black
                textDisabledColor = new Color(0.65f, 0.65f, 0.7f, 1f),
                borderColor = new Color(0.95f, 0.2f, 0.45f, 1f),      // Pink border
                buttonNormal = new Color(0.95f, 0.96f, 0.98f, 1f),
                buttonHighlighted = new Color(0.95f, 0.75f, 0.85f, 1f),
                buttonPressed = new Color(0.9f, 0.92f, 0.96f, 1f),
                buttonDisabled = new Color(0.92f, 0.92f, 0.95f, 0.6f),
            };
        }

        /// <summary>
        /// Returns the built-in Night (dark/neon) variant matching the black mockup.
        /// </summary>
        public static UIThemeVariant CreateNight()
        {
            return new UIThemeVariant
            {
                variantName = "Night",
                mode = Mode.Night,
                primaryColor = new Color(1f, 0.1f, 0.45f, 1f),       // Neon pink
                secondaryColor = new Color(0.2f, 0.85f, 1f, 1f),     // Neon cyan
                accentColor = new Color(1f, 0.85f, 0.2f, 1f),        // Gold stars
                backgroundColor = new Color(0.06f, 0.04f, 0.12f, 1f),// Dark navy
                panelColor = new Color(0.1f, 0.08f, 0.18f, 0.95f),   // Dark purple
                textColor = new Color(1f, 1f, 1f, 1f),               // White
                textDisabledColor = new Color(0.6f, 0.6f, 0.7f, 1f),
                borderColor = new Color(1f, 0.1f, 0.45f, 0.8f),      // Neon pink
                buttonNormal = new Color(0.15f, 0.12f, 0.25f, 1f),
                buttonHighlighted = new Color(0.2f, 0.15f, 0.35f, 1f),
                buttonPressed = new Color(0.1f, 0.08f, 0.2f, 1f),
                buttonDisabled = new Color(0.15f, 0.15f, 0.2f, 0.5f),
            };
        }

        /// <summary>
        /// Copies this variant's colors into a live UITheme asset.
        /// </summary>
        public void BuildInto(UITheme theme)
        {
            if (theme == null) return;
            theme.primaryColor = primaryColor;
            theme.secondaryColor = secondaryColor;
            theme.accentColor = accentColor;
            theme.backgroundColor = backgroundColor;
            theme.panelColor = panelColor;
            theme.textColor = textColor;
            theme.textDisabledColor = textDisabledColor;
            theme.borderColor = borderColor;
            theme.buttonNormal = buttonNormal;
            theme.buttonHighlighted = buttonHighlighted;
            theme.buttonPressed = buttonPressed;
            theme.buttonDisabled = buttonDisabled;
        }
    }
}
