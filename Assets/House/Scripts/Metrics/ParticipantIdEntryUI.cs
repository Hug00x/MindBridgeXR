using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class ParticipantIdEntryUI : MonoBehaviour
{
    private const string LocomotionObjectName = "Locomotion";
    private const int MinimumNumberDigits = 3;
    private const int MaximumInputDigits = 6;

    private readonly StringBuilder enteredDigits = new StringBuilder();
    private Action<string> confirmedCallback;
    private TMP_Text participantIdText;
    private TMP_Text statusText;
    private GameObject locomotionObject;
    private bool locomotionWasActive;
    private bool initialized;

    public static void Show(Action<string> onConfirmed)
    {
        ParticipantIdEntryUI existing =
            FindFirstObjectByType<ParticipantIdEntryUI>(FindObjectsInactive.Include);

        if (existing != null)
        {
            existing.confirmedCallback = onConfirmed;
            return;
        }

        GameObject root = new GameObject(
            "ParticipantIdEntryUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(TrackedDeviceGraphicRaycaster),
            typeof(ParticipantIdEntryUI));

        ParticipantIdEntryUI entryUI = root.GetComponent<ParticipantIdEntryUI>();
        entryUI.confirmedCallback = onConfirmed;
    }

    private void Start()
    {
        StartCoroutine(InitializeWhenCameraIsReady());
    }

    private IEnumerator InitializeWhenCameraIsReady()
    {
        while (Camera.main == null)
            yield return null;

        if (initialized)
            yield break;

        initialized = true;
        EnsureEventSystem();
        DisableLocomotion();
        BuildInterface(Camera.main);
    }

    private void OnDestroy()
    {
        RestoreLocomotion();
    }

    private void BuildInterface(Camera targetCamera)
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = targetCamera;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        scaler.referencePixelsPerUnit = 100f;

        RectTransform canvasRect = GetComponent<RectTransform>();
        canvasRect.SetParent(targetCamera.transform, false);
        canvasRect.sizeDelta = new Vector2(820f, 760f);
        canvasRect.localPosition = new Vector3(0f, -0.03f, 1.55f);
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one * 0.00145f;

        Image background = gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.08f, 0.98f);

        CreateText(
            "Title",
            transform,
            "Identificação do participante",
            new Vector2(0f, 315f),
            new Vector2(740f, 70f),
            42f,
            FontStyles.Bold,
            Color.white);

        CreateText(
            "Instructions",
            transform,
            "Introduz o número anónimo e confirma para começar.",
            new Vector2(0f, 255f),
            new Vector2(740f, 55f),
            27f,
            FontStyles.Normal,
            new Color(0.82f, 0.88f, 0.95f));

        participantIdText = CreateText(
            "ParticipantId",
            transform,
            "P___",
            new Vector2(0f, 185f),
            new Vector2(520f, 75f),
            54f,
            FontStyles.Bold,
            new Color(0.4f, 0.85f, 1f));

        string[,] keypad =
        {
            { "1", "2", "3" },
            { "4", "5", "6" },
            { "7", "8", "9" },
            { "Limpar", "0", "Apagar" }
        };

        float[] columnX = { -180f, 0f, 180f };
        float[] rowY = { 85f, -10f, -105f, -200f };

        for (int row = 0; row < keypad.GetLength(0); row++)
        {
            for (int column = 0; column < keypad.GetLength(1); column++)
            {
                string label = keypad[row, column];
                Button button = CreateButton(
                    "Key_" + label,
                    transform,
                    label,
                    new Vector2(columnX[column], rowY[row]),
                    new Vector2(155f, 76f),
                    new Color(0.12f, 0.22f, 0.34f),
                    31f);

                if (label == "Limpar")
                    button.onClick.AddListener(ClearInput);
                else if (label == "Apagar")
                    button.onClick.AddListener(RemoveLastDigit);
                else
                    button.onClick.AddListener(() => AppendDigit(label[0]));
            }
        }

        Button confirmButton = CreateButton(
            "Confirm",
            transform,
            "Confirmar e começar",
            new Vector2(0f, -305f),
            new Vector2(515f, 82f),
            new Color(0.08f, 0.52f, 0.32f),
            32f);

        confirmButton.onClick.AddListener(ConfirmParticipantId);

        statusText = CreateText(
            "Status",
            transform,
            string.Empty,
            new Vector2(0f, -360f),
            new Vector2(700f, 45f),
            23f,
            FontStyles.Normal,
            new Color(1f, 0.55f, 0.45f));
    }

    private void AppendDigit(char digit)
    {
        if (!char.IsDigit(digit) || enteredDigits.Length >= MaximumInputDigits)
            return;

        enteredDigits.Append(digit);
        UpdateDisplay();
    }

    private void RemoveLastDigit()
    {
        if (enteredDigits.Length == 0)
            return;

        enteredDigits.Length--;
        UpdateDisplay();
    }

    private void ClearInput()
    {
        enteredDigits.Clear();
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (participantIdText == null)
            return;

        participantIdText.text = enteredDigits.Length == 0
            ? "P___"
            : BuildParticipantId();

        if (statusText != null)
            statusText.text = string.Empty;
    }

    private void ConfirmParticipantId()
    {
        if (enteredDigits.Length == 0)
        {
            if (statusText != null)
                statusText.text = "Introduz pelo menos um número.";

            return;
        }

        string participantId = BuildParticipantId();
        Action<string> callback = confirmedCallback;
        confirmedCallback = null;

        RestoreLocomotion();
        callback?.Invoke(participantId);
        Destroy(gameObject);
    }

    private string BuildParticipantId()
    {
        return "P" + enteredDigits.ToString().PadLeft(MinimumNumberDigits, '0');
    }

    private void DisableLocomotion()
    {
        Transform[] transforms =
            FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Transform item in transforms)
        {
            if (item == null || item.gameObject.name != LocomotionObjectName)
                continue;

            locomotionObject = item.gameObject;
            locomotionWasActive = locomotionObject.activeSelf;
            locomotionObject.SetActive(false);
            break;
        }
    }

    private void RestoreLocomotion()
    {
        if (locomotionObject != null)
            locomotionObject.SetActive(locomotionWasActive);

        locomotionObject = null;
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem =
            FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

        if (eventSystem != null)
        {
            if (!eventSystem.gameObject.activeSelf)
                eventSystem.gameObject.SetActive(true);

            if (eventSystem.GetComponent<XRUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<XRUIInputModule>();

            return;
        }

        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(XRUIInputModule));

        DontDestroyOnLoad(eventSystemObject);
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        string content,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        FontStyles fontStyle,
        Color color)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        return text;
    }

    private static Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 anchoredPosition,
        Vector2 size,
        Color normalColor,
        float fontSize)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));

        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = normalColor;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = normalColor * 1.25f;
        colors.pressedColor = normalColor * 0.75f;
        colors.selectedColor = normalColor * 1.1f;
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        CreateText(
            "Label",
            buttonObject.transform,
            label,
            Vector2.zero,
            size,
            fontSize,
            FontStyles.Bold,
            Color.white);

        return button;
    }
}
