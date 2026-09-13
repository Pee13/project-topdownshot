using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDownTacticalAI.UI.Themes;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Flat design level select button.
    /// Uses solid colors without shadows or gradients for a clean modern look.
    /// Has three states: locked (padlock + dark overlay), unlocked, and cleared
    /// (small badge on the card to mark that the player has finished it).
    /// </summary>
    public class LevelSelectFlatButton : MonoBehaviour
    {
        [Header("UI References")]
        public Image backgroundImage;
        public Image previewImage;
        public TMP_Text levelNameText;
        public TMP_Text descriptionText;
        public Button button;
        public GameObject lockedOverlay;
        public Image lockedIcon;
        public GameObject clearedBadge;
        public Image clearedBadgeImage;

        [Header("Flat Design Colors")]
        public Color backgroundColor = new Color(0.18f, 0.22f, 0.32f, 1f);
        public Color highlightedColor = new Color(0.25f, 0.35f, 0.55f, 1f);
        public Color pressedColor = new Color(0.12f, 0.18f, 0.3f, 1f);
        public Color lockedColor = new Color(0.12f, 0.13f, 0.18f, 1f);
        public Color textColor = Color.white;

        private System.Action<LevelSelectController.LevelData, int> _onClickCallback;
        private LevelSelectController.LevelData _levelData;
        private int _index;

        private void Awake()
        {
            if (levelNameText != null)
            {
                levelNameText.color = textColor;
                levelNameText.fontSize = 28;
                levelNameText.alignment = TextAlignmentOptions.Center;
                levelNameText.enableAutoSizing = true;
                levelNameText.fontSizeMin = 20;
                levelNameText.fontSizeMax = 40;
            }

            if (descriptionText != null)
            {
                descriptionText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                descriptionText.fontSize = 18;
                descriptionText.alignment = TextAlignmentOptions.Center;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = backgroundColor;
                backgroundImage.type = Image.Type.Simple;
            }

            if (button != null)
            {
                var colors = button.colors;
                colors.normalColor = backgroundColor;
                colors.highlightedColor = highlightedColor;
                colors.pressedColor = pressedColor;
                colors.disabledColor = new Color(0.15f, 0.15f, 0.2f, 0.5f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.1f;
                button.colors = colors;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnButtonClick);
            }

            if (lockedOverlay != null) lockedOverlay.SetActive(false);
            if (clearedBadge != null) clearedBadge.SetActive(false);
        }

        public void Setup(LevelSelectController.LevelData levelData, int index, bool isUnlocked, bool isCleared,
            System.Action<LevelSelectController.LevelData, int> onClick)
        {
            _levelData = levelData;
            _index = index;
            _onClickCallback = onClick;

            if (backgroundImage != null)
            {
                backgroundImage.color = isUnlocked ? backgroundColor : lockedColor;
            }

            if (previewImage != null && levelData.previewImage != null)
            {
                previewImage.sprite = levelData.previewImage;
                previewImage.type = Image.Type.Simple;
                previewImage.preserveAspect = true;
            }

            if (levelNameText != null)
            {
                int displayIndex = index + 1;
                levelNameText.text = $"STAGE {displayIndex:00}";
            }

            if (descriptionText != null)
            {
                descriptionText.text = levelData.description;
            }

            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(!isUnlocked);
            }

            if (lockedIcon != null && levelData.lockedImage != null)
            {
                lockedIcon.sprite = levelData.lockedImage;
            }

            if (clearedBadge != null)
            {
                clearedBadge.SetActive(isUnlocked && isCleared);
            }

            if (clearedBadgeImage != null && levelData.clearedImage != null)
            {
                clearedBadgeImage.sprite = levelData.clearedImage;
            }
        }

        /// <summary>
        /// Updates the unlock / cleared visuals in place without rebuilding
        /// the button. Called by <see cref="LevelSelectController"/> after a
        /// stage is cleared so the next card immediately unlocks.
        /// </summary>
        public void SetState(bool isUnlocked, bool isCleared, LevelSelectController.LevelData levelData)
        {
            _levelData = levelData;

            if (backgroundImage != null)
            {
                backgroundImage.color = isUnlocked ? backgroundColor : lockedColor;
            }

            if (lockedOverlay != null) lockedOverlay.SetActive(!isUnlocked);
            if (clearedBadge != null) clearedBadge.SetActive(isUnlocked && isCleared);

            if (levelData != null)
            {
                if (lockedIcon != null && levelData.lockedImage != null)
                {
                    lockedIcon.sprite = levelData.lockedImage;
                }
                if (clearedBadgeImage != null && levelData.clearedImage != null)
                {
                    clearedBadgeImage.sprite = levelData.clearedImage;
                }
            }
        }

        public void ApplyThemeColors(UITheme theme)
        {
            if (theme == null) return;

            if (backgroundImage != null) backgroundImage.color = theme.panelColor;
            if (levelNameText != null) levelNameText.color = theme.textColor;
            if (descriptionText != null) descriptionText.color = theme.textDisabledColor;
        }

        private void OnButtonClick()
        {
            _onClickCallback?.Invoke(_levelData, _index);
        }
    }
}
