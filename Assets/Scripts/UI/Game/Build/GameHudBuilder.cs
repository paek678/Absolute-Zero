using AbsoluteZero.Core.Common;
using AbsoluteZero.UI.Emote;
using TMPro;
using UnityEngine;
using AbsoluteZero.UI.Common;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Game.Build
{
    public static class GameHudBuilder
    {
        static readonly Color INIT_HP_COLOR = new(0.39f, 0.78f, 0.31f, 1f);
        static readonly Vector3 READY_BTN_POS = new(0f, 0.35f, 1.2f);
        const float WORLD_CANVAS_SCALE = 0.005f;
        const float OPP_BAR_SCALE = 0.007f;

        public static GameHudRefs Build(int seatCount = 2)
        {
            var r = new GameHudRefs();
            BuildOverlayUI(r, seatCount);
            BuildOppBarWorldUI(r, seatCount > 2);
            BuildReadyWorldUI(r);
            foreach (var bar in r.OppBars)
                AbsoluteZero.Core.Combat.DuelSpriteFacing.RegisterWorldUi(bar.Canvas);
            AbsoluteZero.Core.Combat.DuelSpriteFacing.RegisterWorldUi(r.ReadyCanvas);
            return r;
        }

        static void BuildOverlayUI(GameHudRefs r, int seatCount)
        {
            var canvasGO = new GameObject("OverlayCanvas");
            r.OverlayCanvas = canvasGO.AddComponent<Canvas>();
            r.OverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            r.OverlayCanvas.sortingOrder = 0;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0f;

            canvasGO.AddComponent<GraphicRaycaster>();

            Transform root = canvasGO.transform;

            BuildMyHpBar(r, root);
            BuildClockTimer(r, root);

            r.PhaseText = CreateText(root, "PhaseText",
                new Vector2(0, -30), new Vector2(400, 50), "참가자 대기", 28);
            AnchorTopCenter(r.PhaseText.GetComponent<RectTransform>());

            BuildProgressHud(r, root, seatCount);

            r.StatusText = CreateText(root, "StatusText",
                new Vector2(0, 30), new Vector2(600, 35), "Waiting for players...", 20);
            r.StatusText.color = new Color(0.8f, 0.8f, 0.8f);
            AnchorBottomCenter(r.StatusText.GetComponent<RectTransform>());

            bool showDiagnostics = seatCount <= 2 || GameplayDebugDisplay.Enabled;
            r.PhaseText.gameObject.SetActive(showDiagnostics);
            r.StatusText.gameObject.SetActive(showDiagnostics);

            BuildEnvironmentPanel(r, root);
            BuildCinematicOverlay(r, root);
            BuildGhostSkillPanel(r, root);
        }

        static void BuildMyHpBar(GameHudRefs r, Transform root)
        {
            var container = new GameObject("MyHpBar");
            container.transform.SetParent(root, false);
            var cRect = container.AddComponent<RectTransform>();
            cRect.anchoredPosition = new Vector2(395.8f, -81.9f);
            cRect.sizeDelta = new Vector2(600, 100);
            cRect.anchorMin = new Vector2(0f, 1f);
            cRect.anchorMax = new Vector2(0f, 1f);
            cRect.pivot = new Vector2(0.5f, 0.5f);

            r.MyHpSlider = container.AddComponent<Slider>();
            r.MyHpSlider.minValue = 0f;
            r.MyHpSlider.maxValue = 1f;
            r.MyHpSlider.value = 1f;
            r.MyHpSlider.interactable = false;
            r.MyHpSlider.direction = Slider.Direction.LeftToRight;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(container.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.sprite = GameSprites.Get(GameSprites.UI_BAR_BG);
            bgImg.color = Color.white;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(container.transform, false);
            var faRect = fillArea.AddComponent<RectTransform>();
            faRect.anchorMin = new Vector2(0f, 0.25f);
            faRect.anchorMax = new Vector2(1f, 0.75f);
            faRect.offsetMin = Vector2.zero;
            faRect.offsetMax = Vector2.zero;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillArea.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = GameSprites.Get(GameSprites.UI_BAR_FILL);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.color = INIT_HP_COLOR;
            r.MyHpFillImage = fillImg;

            r.MyHpSlider.fillRect = fillRect;

            var outlineGO = new GameObject("Outline");
            outlineGO.transform.SetParent(container.transform, false);
            var olRect = outlineGO.AddComponent<RectTransform>();
            olRect.anchoredPosition = new Vector2(3.6f, 0f);
            olRect.sizeDelta = new Vector2(620, 80);
            var olImg = outlineGO.AddComponent<Image>();
            olImg.sprite = GameSprites.Get(GameSprites.UI_BAR_OUTLINE);
            olImg.raycastTarget = false;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(container.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(-330.79f, -25.85f);
            iconRect.sizeDelta = new Vector2(100, 200);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = GameSprites.Get(GameSprites.UI_THERMO_ICON);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            BuildGiftLines(container.transform);

        }

        static void BuildGiftLines(Transform parent)
        {
            var giftRoot = new GameObject("GiftLine");
            giftRoot.transform.SetParent(parent, false);
            var grRect = giftRoot.AddComponent<RectTransform>();
            grRect.anchorMin = Vector2.zero;
            grRect.anchorMax = Vector2.one;
            grRect.offsetMin = Vector2.zero;
            grRect.offsetMax = Vector2.zero;

            float[] xPositions = { 159.1f, 324.6f, 485.8f };
            string[] iconNames = { GameSprites.UI_GIFT_ICON_A, GameSprites.UI_GIFT_ICON_B, GameSprites.UI_GIFT_ICON_C };
            Vector2[] iconSizes = { new(70, 70), new(70, 35), new(35, 35) };
            float[] iconYOffsets = { -50f, -30f, -30f };

            for (int i = 0; i < 3; i++)
            {
                var lineGO = new GameObject($"line_{(i + 1) * 10}");
                lineGO.transform.SetParent(giftRoot.transform, false);
                var lineRect = lineGO.AddComponent<RectTransform>();
                lineRect.anchorMin = new Vector2(0f, 0.5f);
                lineRect.anchorMax = new Vector2(0f, 0.5f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);
                lineRect.anchoredPosition = new Vector2(xPositions[i], 0f);
                lineRect.sizeDelta = new Vector2(7.45f, 70f);
                var lineImg = lineGO.AddComponent<Image>();
                lineImg.sprite = GameSprites.Get(GameSprites.UI_GIFT_LINE);
                lineImg.raycastTarget = false;

                var giftGO = new GameObject("Icon_Gift");
                giftGO.transform.SetParent(lineGO.transform, false);
                var giftRect = giftGO.AddComponent<RectTransform>();
                giftRect.anchoredPosition = new Vector2(0f, iconYOffsets[i]);
                giftRect.sizeDelta = iconSizes[i];
                var giftImg = giftGO.AddComponent<Image>();
                giftImg.sprite = GameSprites.Get(iconNames[i]);
                giftImg.raycastTarget = false;
            }
        }

        static void BuildClockTimer(GameHudRefs r, Transform root)
        {
            var container = new GameObject("Timer");
            container.transform.SetParent(root, false);
            var cRect = container.AddComponent<RectTransform>();
            cRect.anchoredPosition = new Vector2(-108, -108);
            cRect.sizeDelta = new Vector2(204, 206);
            cRect.localScale = new Vector3(0.7f, 0.7f, 1f);
            AnchorTopRight(cRect);
            r.TimerContainerRT = cRect;

            var timerBg = new GameObject("TimerBg");
            timerBg.transform.SetParent(container.transform, false);
            var tbRect = timerBg.AddComponent<RectTransform>();
            tbRect.anchoredPosition = Vector2.zero;
            tbRect.sizeDelta = new Vector2(204, 206);
            r.TimerFillImage = timerBg.AddComponent<Image>();
            r.TimerFillImage.sprite = GameSprites.Get(GameSprites.UI_TIMER_BG);
            r.TimerFillImage.type = Image.Type.Simple;
            r.TimerFillImage.preserveAspect = true;
            r.TimerFillImage.color = Color.white;
            r.TimerFillImage.raycastTarget = false;

            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(container.transform, false);
            var slRect = sliderGO.AddComponent<RectTransform>();
            slRect.anchoredPosition = Vector2.zero;
            slRect.sizeDelta = new Vector2(204, 206);
            r.TimerSliderImage = sliderGO.AddComponent<Image>();
            r.TimerSliderImage.sprite = GameSprites.Get(GameSprites.UI_TIMER_BG);
            r.TimerSliderImage.type = Image.Type.Filled;
            r.TimerSliderImage.fillMethod = Image.FillMethod.Radial360;
            r.TimerSliderImage.fillOrigin = 2;
            r.TimerSliderImage.fillClockwise = true;
            r.TimerSliderImage.fillAmount = 0f;
            r.TimerSliderImage.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            r.TimerSliderImage.raycastTarget = false;

            var outlineGO = new GameObject("Outline");
            outlineGO.transform.SetParent(container.transform, false);
            var olRect = outlineGO.AddComponent<RectTransform>();
            olRect.anchoredPosition = Vector2.zero;
            olRect.sizeDelta = new Vector2(261, 261);
            var olImg = outlineGO.AddComponent<Image>();
            olImg.sprite = GameSprites.Get(GameSprites.UI_TIMER_OUTLINE);
            olImg.raycastTarget = false;

            var lineGO = new GameObject("Line");
            lineGO.transform.SetParent(container.transform, false);
            var liRect = lineGO.AddComponent<RectTransform>();
            liRect.anchoredPosition = Vector2.zero;
            liRect.sizeDelta = new Vector2(191, 193);
            var liImg = lineGO.AddComponent<Image>();
            liImg.sprite = GameSprites.Get(GameSprites.UI_TIMER_LINE);
            liImg.raycastTarget = false;

            var handGO = new GameObject("ClockHand");
            handGO.transform.SetParent(container.transform, false);
            r.ClockHandRT = handGO.AddComponent<RectTransform>();
            r.ClockHandRT.anchoredPosition = Vector2.zero;
            r.ClockHandRT.sizeDelta = new Vector2(33, 114);
            r.ClockHandRT.pivot = new Vector2(0.5f, 0.15f);
            var handImg = handGO.AddComponent<Image>();
            handImg.sprite = GameSprites.Get(GameSprites.UI_TIMER_HAND);
            handImg.raycastTarget = false;

            r.TimerText = CreateText(container.transform, "TimerText",
                new Vector2(0, -10), new Vector2(120, 60), "", 36);
            r.TimerText.color = Color.white;
            r.TimerText.fontStyle = FontStyles.Bold;

        }

        static void BuildOppBarWorldUI(GameHudRefs r, bool multi)
        {
            const int MAX_OPP_BARS = 3;
            for (int i = 0; i < MAX_OPP_BARS; i++)
            {
                var entry = BuildSingleOppBar(i, multi ? 0.8f : 1f);
                r.OppBars.Add(entry);
                entry.Canvas.gameObject.SetActive(false);
            }

            var first = r.OppBars[0];
            r.OppBarCanvas = first.Canvas;
            r.OppHpSlider = first.HpSlider;
            r.OppHpFillImage = first.HpFillImage;
        }

        static OppBarEntry BuildSingleOppBar(int index, float scale)
        {
            var canvasGO = new GameObject($"EnemyCanvas_{index}");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 100);
            canvasGO.transform.localScale = Vector3.one * (OPP_BAR_SCALE * scale);

            canvasGO.AddComponent<CanvasScaler>();
            // Display-only world HUD. Its invisible text rect must not intercept
            // clicks aimed at the character beneath it.
            canvasGO.AddComponent<CanvasGroup>().blocksRaycasts = false;

            var hpBar = new GameObject("HPBar");
            hpBar.transform.SetParent(canvasGO.transform, false);
            var hpRect = hpBar.AddComponent<RectTransform>();
            hpRect.anchoredPosition = Vector2.zero;
            hpRect.sizeDelta = new Vector2(600, 100);

            var slider = hpBar.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(hpBar.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.sprite = GameSprites.Get(GameSprites.UI_BAR_BG);
            bgImg.color = Color.white;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(hpBar.transform, false);
            var faRect = fillArea.AddComponent<RectTransform>();
            faRect.anchorMin = new Vector2(0f, 0.25f);
            faRect.anchorMax = new Vector2(1f, 0.75f);
            faRect.offsetMin = Vector2.zero;
            faRect.offsetMax = Vector2.zero;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillArea.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = GameSprites.Get(GameSprites.UI_BAR_FILL);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.color = INIT_HP_COLOR;

            slider.fillRect = fillRect;

            var outlineGO = new GameObject("Outline");
            outlineGO.transform.SetParent(hpBar.transform, false);
            var olRect = outlineGO.AddComponent<RectTransform>();
            olRect.anchoredPosition = new Vector2(3.6f, 0f);
            olRect.sizeDelta = new Vector2(620, 80);
            var olImg = outlineGO.AddComponent<Image>();
            olImg.sprite = GameSprites.Get(GameSprites.UI_BAR_OUTLINE);
            olImg.raycastTarget = false;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(hpBar.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(-302f, -26f);
            iconRect.sizeDelta = new Vector2(100, 200);
            iconRect.localScale = new Vector3(-1f, 1f, 1f);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = GameSprites.Get(GameSprites.UI_THERMO_ICON);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            BuildOppDividerLines(hpBar.transform);

            return new OppBarEntry
            {
                Canvas = canvas,
                HpSlider = slider,
                HpFillImage = fillImg
            };
        }

        static void BuildOppDividerLines(Transform parent)
        {
            var root = new GameObject("DividerLines");
            root.transform.SetParent(parent, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            float[] xPositions = { 159.1f, 324.6f, 485.8f };
            for (int i = 0; i < 3; i++)
            {
                var lineGO = new GameObject($"line_{(i + 1) * 10}");
                lineGO.transform.SetParent(root.transform, false);
                var lineRect = lineGO.AddComponent<RectTransform>();
                lineRect.anchorMin = new Vector2(0f, 0.5f);
                lineRect.anchorMax = new Vector2(0f, 0.5f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);
                lineRect.anchoredPosition = new Vector2(xPositions[i], 0f);
                lineRect.sizeDelta = new Vector2(7.45f, 70f);
                var lineImg = lineGO.AddComponent<Image>();
                lineImg.sprite = GameSprites.Get(GameSprites.UI_GIFT_LINE);
                lineImg.raycastTarget = false;
            }
        }

        static void BuildReadyWorldUI(GameHudRefs r)
        {
            var canvasGO = new GameObject("ReadyWorldCanvas");
            r.ReadyCanvas = canvasGO.AddComponent<Canvas>();
            r.ReadyCanvas.renderMode = RenderMode.WorldSpace;
            r.ReadyCanvas.overrideSorting = true;
            r.ReadyCanvas.sortingOrder = WorldInteractionLayer.ButtonOrder;

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 180);
            canvasGO.transform.localScale = Vector3.one * WORLD_CANVAS_SCALE;
            canvasGO.transform.position = READY_BTN_POS;
            canvasGO.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

            canvasGO.AddComponent<GraphicRaycaster>();

            var readySprite = GameSprites.Get(GameSprites.BTN_DEFAULT);
            var btnGO = new GameObject("ReadyBtn");
            btnGO.transform.SetParent(canvasGO.transform, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchoredPosition = Vector2.zero;
            btnRect.sizeDelta = new Vector2(560, 320);

            var btnImg = btnGO.AddComponent<Image>();
            btnImg.material = WorldInteractionLayer.Material;
            if (readySprite != null)
            {
                btnImg.sprite = readySprite;
                btnImg.preserveAspect = true;
            }
            else
            {
                btnImg.color = new Color(0.5f, 0.5f, 0.2f);
            }

            r.ReadyButtonImage = btnImg;
            r.ReadyButton = btnGO.AddComponent<Button>();
            r.ReadyButton.targetGraphic = btnImg;
            btnGO.AddComponent<EmoteWheel>();
            r.ReadyCanvas.gameObject.SetActive(false);
        }

        static void BuildProgressHud(GameHudRefs r, Transform root, int seatCount)
        {
            seatCount = Mathf.Clamp(seatCount, 2, 4);
            const float gap = 10, cardHeight = 84;
            float width = seatCount == 2 ? 360 : 530;
            float cardWidth = (width - gap * (seatCount - 1)) / seatCount;
            var container = new GameObject("ProgressHud", typeof(RectTransform));
            container.transform.SetParent(root, false);
            var rect = container.GetComponent<RectTransform>();
            AnchorTopCenter(rect);
            // The wider Multi row otherwise overlaps the authored HP outline.
            rect.anchoredPosition = new Vector2(seatCount > 2 ? 24 : 0, -120);
            rect.sizeDelta = new Vector2(width, cardHeight);
            r.NameBoxes = new Image[seatCount]; r.NameTexts = new TextMeshProUGUI[seatCount];
            r.SeatLabels = new TextMeshProUGUI[seatCount]; r.ActionLabels = new TextMeshProUGUI[seatCount];
            r.ScoreLabels = new TextMeshProUGUI[seatCount];
            r.ActionBadges = new Image[seatCount]; r.ActiveFrames = new Image[seatCount];
            r.ActionBadgeRoots = new GameObject[seatCount];
            var bannerFrame = Resources.Load<Sprite>("UI/ParticipantBannerFrame");
            var bannerFont = Resources.Load<TMP_FontAsset>("UI/Fonts/GmarketBanner");
            var ink = new Color(.23f, .20f, .18f);
            for (int i = 0; i < seatCount; i++)
            {
                float x = -width / 2 + cardWidth / 2 + i * (cardWidth + gap);
                var box = CreatePanel(rect, $"P{i}Box", new Vector2(x, 0), new Vector2(cardWidth, cardHeight), Color.clear);
                box.raycastTarget = false;
                var activeFrame = CreatePanel(box.transform, "ActiveHighlight", Vector2.zero,
                    box.rectTransform.sizeDelta + new Vector2(4, 4), new Color(1f, .74f, .27f));
                activeFrame.sprite = bannerFrame;
                activeFrame.type = Image.Type.Sliced;
                activeFrame.raycastTarget = false;
                activeFrame.enabled = false;
                r.ActiveFrames[i] = activeFrame;
                r.NameBoxes[i] = BuildCartoonBanner(box.rectTransform, bannerFrame, new Color(.74f, .86f, .82f));
                var badge = CreatePanel(box.transform, "ActionBadge", new Vector2(0, -22),
                    new Vector2(cardWidth - 24, 24), Color.clear);
                badge.raycastTarget = false;
                badge.gameObject.AddComponent<RectMask2D>();
                r.ActionBadgeRoots[i] = badge.gameObject;
                r.ActionBadges[i] = CreatePanel(badge.transform, "BadgeFill", Vector2.zero,
                    badge.rectTransform.sizeDelta - new Vector2(8, 8), new Color(1f, .94f, .76f));
                r.ActionBadges[i].raycastTarget = false;
                var badgeBorder = CreatePanel(badge.transform, "BadgeBorder", Vector2.zero,
                    badge.rectTransform.sizeDelta, Color.white);
                badgeBorder.sprite = bannerFrame;
                badgeBorder.type = Image.Type.Sliced;
                badgeBorder.pixelsPerUnitMultiplier = 2.5f;
                badgeBorder.raycastTarget = false;
                // Gmarket's full-height Korean glyphs need smaller point sizes than
                // Dongle. Keep the identity, nickname and action rows visually separate.
                r.SeatLabels[i] = CreateText(box.transform, "Seat", new Vector2(-19, 28.5f), new Vector2(cardWidth - 62, 22), $"P{i + 1}", 11);
                r.SeatLabels[i].enableAutoSizing = true; r.SeatLabels[i].fontSizeMin = 8; r.SeatLabels[i].fontSizeMax = 11;
                r.ScoreLabels[i] = CreateText(box.transform, "Score", new Vector2(cardWidth / 2 - 28, 28.5f), new Vector2(36, 22), "", 14);
                r.ScoreLabels[i].enableAutoSizing = true; r.ScoreLabels[i].fontSizeMin = 10; r.ScoreLabels[i].fontSizeMax = 14;
                r.NameTexts[i] = CreateText(box.transform, $"P{i}Name", new Vector2(0, 6), new Vector2(cardWidth - 16, 36), "빈 자리", 22);
                r.NameTexts[i].enableAutoSizing = true; r.NameTexts[i].fontSizeMin = 12; r.NameTexts[i].fontSizeMax = 22;
                r.ActionLabels[i] = CreateText(badge.transform, "Action", Vector2.zero,
                    new Vector2(badge.rectTransform.sizeDelta.x - 16, 20), "", 14);
                r.ActionLabels[i].enableAutoSizing = true;
                r.ActionLabels[i].fontSizeMin = 10;
                r.ActionLabels[i].fontSizeMax = 14;
                foreach (var label in new[] { r.SeatLabels[i], r.ScoreLabels[i], r.NameTexts[i], r.ActionLabels[i] })
                {
                    if (bannerFont != null) label.font = bannerFont;
                    label.alignment = TextAlignmentOptions.Midline;
                    label.raycastTarget = false; label.richText = false; label.color = ink;
                    label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
                }
                StyleBannerTitle(r.NameTexts[i]);
                r.SeatLabels[i].fontStyle = FontStyles.Normal;
                r.SeatLabels[i].alignment = TextAlignmentOptions.MidlineLeft;
                r.SeatLabels[i].color = new Color(.34f, .30f, .26f);
                r.ScoreLabels[i].alignment = TextAlignmentOptions.MidlineRight;
                StyleBannerTitle(r.ScoreLabels[i]);
                r.ActionLabels[i].fontStyle = FontStyles.Normal;
                badge.gameObject.SetActive(false);
            }
            r.P1NameBox = r.NameBoxes[0]; r.P2NameBox = r.NameBoxes[1];
            r.P1NameText = r.NameTexts[0]; r.P2NameText = r.NameTexts[1];
            // Keep the round above the cards, with room below the phase diagnostics.
            // The row's bottom remains above the former combined score ribbon.
            var roundPanel = CreatePanel(rect, "RoundRibbon", new Vector2(0, 65), new Vector2(138, 26), Color.clear);
            roundPanel.raycastTarget = false;
            BuildCartoonBanner(roundPanel.rectTransform, bannerFrame, new Color(.96f, .89f, .75f));
            r.RoundText = CreateText(roundPanel.transform, "RoundText", Vector2.zero, new Vector2(122, 20), "라운드 0", 14);
            r.RoundText.raycastTarget = false;
            r.RoundText.richText = false;
            r.RoundText.textWrappingMode = TextWrappingModes.NoWrap;
            if (bannerFont != null) r.RoundText.font = bannerFont;
            r.RoundText.alignment = TextAlignmentOptions.Midline;
            r.RoundText.color = ink;
            StyleBannerTitle(r.RoundText);
        }

        static void StyleBannerTitle(TextMeshProUGUI label)
        {
            // The selected font already supplies its native Bold weight.
            // Do not stack TMP synthetic bold on top and close the small counters.
            label.fontStyle = FontStyles.Normal;
            label.characterSpacing = 0;
            // Vertex colors preserve the shared font material and Korean fallback atlas.
            label.color = Color.white;
            label.enableVertexGradient = true;
            var top = new Color(.35f, .27f, .20f);
            var bottom = new Color(.16f, .12f, .10f);
            label.colorGradient = new VertexGradient(top, top, bottom, bottom);
        }

        // Generated frame and solid fill are separate: active-seat colors never tint
        // the white outline, and the generated center's alpha never obscures text.
        static Image BuildCartoonBanner(RectTransform root, Sprite frame, Color color)
        {
            bool compact = root.sizeDelta.y < 40;
            var shadow = CreatePanel(root, "BannerShadow", new Vector2(0, -3), root.sizeDelta,
                new Color(.12f, .16f, .17f, .35f));
            shadow.sprite = frame;
            shadow.type = Image.Type.Sliced;
            shadow.pixelsPerUnitMultiplier = compact ? 1.5f : 1f;
            shadow.raycastTarget = false;
            if (frame == null) shadow.enabled = false;
            var fill = CreatePanel(root, "PastelFill", Vector2.zero, root.sizeDelta - new Vector2(10, 10), color);
            fill.raycastTarget = false;
            float sheenHeight = compact ? 7 : 24;
            var sheen = CreatePanel(root, "TopSheen", new Vector2(0, root.sizeDelta.y / 2 - 7 - sheenHeight / 2),
                new Vector2(root.sizeDelta.x - 16, sheenHeight), new Color(1f, 1f, 1f, .23f));
            sheen.raycastTarget = false;
            var glint = CreatePanel(root, "TopGlint", new Vector2(0, root.sizeDelta.y / 2 - 8),
                new Vector2(root.sizeDelta.x - 30, 2), new Color(1f, 1f, 1f, .72f));
            glint.raycastTarget = false;
            var border = CreatePanel(root, "WhiteFrame", Vector2.zero, root.sizeDelta, Color.white);
            border.raycastTarget = false;
            border.sprite = frame;
            border.type = Image.Type.Sliced;
            // The thinner score strip uses the same source with smaller corners.
            border.pixelsPerUnitMultiplier = compact ? 1.5f : 1f;
            if (frame == null) border.color = Color.clear;
            return fill;
        }

        static void BuildCinematicOverlay(GameHudRefs r, Transform root)
        {
            var overlayGO = new GameObject("CinematicOverlay");
            overlayGO.transform.SetParent(root, false);
            var oRect = overlayGO.AddComponent<RectTransform>();
            oRect.anchorMin = Vector2.zero;
            oRect.anchorMax = Vector2.one;
            oRect.offsetMin = Vector2.zero;
            oRect.offsetMax = Vector2.zero;
            r.CinematicOverlay = overlayGO.AddComponent<Image>();
            r.CinematicOverlay.color = new Color(0f, 0f, 0f, 0f);
            r.CinematicOverlay.raycastTarget = false;

            r.CinematicPlate = CreatePanel(overlayGO.transform, "ResultPlate",
                new Vector2(0, 20), new Vector2(880, 160), new Color(1, 1, 1, 0));
            CasualUiSkin.Surface(r.CinematicPlate, new Color(1, 1, 1, 0));
            r.CinematicPlate.raycastTarget = false;
            r.CinematicText = CreateText(overlayGO.transform, "CinematicText",
                new Vector2(0, 20), new Vector2(800, 100), "", 64);
            CasualUiSkin.Label(r.CinematicText);
            r.CinematicText.enableAutoSizing = true; r.CinematicText.fontSizeMin = 32; r.CinematicText.fontSizeMax = 64;
            r.CinematicText.color = new Color(1f, 1f, 1f, 0f);

            r.LobbyButton = CreateButton(overlayGO.transform, "LobbyBtn",
                new Vector2(150, -110), new Vector2(270, 60), "로비로",
                new Color(0.5f, 0.3f, 0.3f));
            r.LobbyButton.gameObject.SetActive(false);

            r.RematchButton = CreateButton(overlayGO.transform, "RematchBtn",
                new Vector2(-150, -110), new Vector2(270, 60), "재대결",
                new Color(0.3f, 0.5f, 0.3f));
            r.RematchButton.gameObject.SetActive(false);

            r.RematchStatusText = CreateText(overlayGO.transform, "RematchStatus",
                new Vector2(0, -170), new Vector2(820, 75), "", 18);
            r.RematchStatusText.gameObject.SetActive(false);
            CasualUiSkin.Button(r.LobbyButton, CasualUiSkin.Peach);
            CasualUiSkin.Button(r.RematchButton, CasualUiSkin.Mint);
            CasualUiSkin.Label(r.RematchStatusText, new Color(.96f, .94f, .88f));

            overlayGO.SetActive(false);
        }

        static void BuildGhostSkillPanel(GameHudRefs r, Transform root)
        {
            var panel = new GameObject("GhostSkillPanel");
            panel.transform.SetParent(root, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchoredPosition = new Vector2(0, 140);
            panelRect.sizeDelta = new Vector2(420, 100);
            AnchorBottomCenter(panelRect);

            CreatePanel(panel.transform, "GhostBg",
                Vector2.zero, new Vector2(420, 100),
                new Color(0.1f, 0.05f, 0.15f, 0.85f));

            r.GhostStatusText = CreateText(panel.transform, "GhostStatus",
                new Vector2(0, 65), new Vector2(400, 30), "", 16);
            r.GhostStatusText.color = new Color(0.7f, 0.8f, 1f);

            r.FrostStrikeButton = CreateButton(panel.transform, "FrostStrikeBtn",
                new Vector2(-100, 12), new Vector2(190, 50), "귀신의 한",
                new Color(0.2f, 0.4f, 0.7f));

            r.FrostStrikeCooldownText = CreateText(panel.transform, "FrostCD",
                new Vector2(-100, -25), new Vector2(190, 20), "", 14);
            r.FrostStrikeCooldownText.color = new Color(0.6f, 0.7f, 1f);

            r.ChillAuraButton = CreateButton(panel.transform, "ChillAuraBtn",
                new Vector2(100, 12), new Vector2(190, 50), "빙의",
                new Color(0.3f, 0.2f, 0.6f));

            r.ChillAuraCooldownText = CreateText(panel.transform, "ChillCD",
                new Vector2(100, -25), new Vector2(190, 20), "", 14);
            r.ChillAuraCooldownText.color = new Color(0.6f, 0.7f, 1f);

            CasualUiSkin.At(panel.transform, "GhostBg", CasualUiSkin.Lavender);
            CasualUiSkin.Labels(panel.transform);
            CasualUiSkin.Button(r.FrostStrikeButton, CasualUiSkin.Sky);
            CasualUiSkin.Button(r.ChillAuraButton, CasualUiSkin.Lavender);
            AddGhostSkillIcon(r.FrostStrikeButton, "skill_han");
            AddGhostSkillIcon(r.ChillAuraButton, "skill_capture");
            r.GhostSkillPanel = panel;
            panel.SetActive(false);
        }

        static void AddGhostSkillIcon(Button button, string resource)
        {
            var sprite = Resources.Load<Sprite>(resource);
            if (sprite == null) return;
            var iconObject = new GameObject("SkillIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            iconObject.transform.SetParent(button.transform, false);
            var icon = iconObject.GetComponent<UnityEngine.UI.Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var rect = icon.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(30f, 0f);
            rect.sizeDelta = new Vector2(42f, 42f);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.rectTransform.offsetMin = new Vector2(56f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
        }

        static void BuildEnvironmentPanel(GameHudRefs r, Transform root)
        {
            r.EnvPanel = new GameObject("EnvPanel");
            r.EnvPanel.transform.SetParent(root, false);
            var rect = r.EnvPanel.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, -140);
            rect.sizeDelta = new Vector2(600, 80);
            AnchorTopCenter(rect);

            CreatePanel(r.EnvPanel.transform, "EnvBg",
                Vector2.zero, new Vector2(600, 80),
                new Color(0.1f, 0.05f, 0.2f, 0.9f));

            r.EnvText = CreateText(r.EnvPanel.transform, "EnvText",
                Vector2.zero, new Vector2(560, 60), "", 32);
            r.EnvText.fontStyle = FontStyles.Bold;
            r.EnvText.color = new Color(1f, 0.85f, 0.3f);

            CasualUiSkin.At(r.EnvPanel.transform, "EnvBg", CasualUiSkin.Butter);
            CasualUiSkin.Label(r.EnvText);
            r.EnvPanel.SetActive(false);
        }

        public static void AnchorTopCenter(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
        }

        public static void AnchorTopLeft(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
        }

        public static void AnchorTopRight(RectTransform rt)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
        }

        public static void AnchorBottomLeft(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
        }

        public static void AnchorBottomCenter(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name,
            Vector2 pos, Vector2 size, string text, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        public static Image CreatePanel(Transform parent, string name,
            Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Button CreateButton(Transform parent, string name,
            Vector2 pos, Vector2 size, string label, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.7f;
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            btn.colors = colors;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }
    }
}
