using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/*
 * Interface em world-space para introduzir o identificador anónimo do participante.
 * Bloqueia temporariamente a locomoção, constrói um teclado numérico em runtime
 * e devolve o ID confirmado ao sistema de métricas.
 */
public class ParticipantIdEntryUI : MonoBehaviour
{
    // Regras de identificação e nome do objeto de locomoção a suspender.
    private const string LocomotionObjectName = "Locomotion";
    private const int MinimumNumberDigits = 3;
    private const int MaximumInputDigits = 6;

    // Estado da entrada atual e referências aos elementos criados em runtime.
    private readonly StringBuilder enteredDigits = new StringBuilder();
    private Action<string> confirmedCallback;
    private TMP_Text participantIdText;
    private TMP_Text statusText;
    private GameObject locomotionObject;
    private Texture2D roundedRectangleTexture;
    private Sprite roundedRectangleSprite;
    private bool locomotionWasActive;
    private bool initialized;

    // Cria a UI apenas uma vez e guarda o callback de confirmação.
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

    // Aguarda pela câmara principal antes de posicionar o painel no espaço XR.
    private void Start()
    {
        StartCoroutine(InitializeWhenCameraIsReady());
    }

    // Prepara EventSystem, suspende locomoção e constrói a interface.
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

    // Restaura recursos temporários quando a UI é destruída.
    private void OnDestroy()
    {
        RestoreLocomotion();

        if (roundedRectangleSprite != null)
            Destroy(roundedRectangleSprite);

        if (roundedRectangleTexture != null)
            Destroy(roundedRectangleTexture);
    }

    // Constrói o painel, textos, teclado e botão de confirmação em world-space.
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
        canvasRect.localPosition = new Vector3(0f, -0.13f, 1.55f);
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one * 0.00145f;

        Image background = gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.08f, 0.98f);
        ApplyRoundedCorners(background);

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

    // Acrescenta um dígito respeitando o limite máximo.
    private void AppendDigit(char digit)
    {
        if (!char.IsDigit(digit) || enteredDigits.Length >= MaximumInputDigits)
            return;

        enteredDigits.Append(digit);
        UpdateDisplay();
    }

    // Remove o último dígito introduzido.
    private void RemoveLastDigit()
    {
        if (enteredDigits.Length == 0)
            return;

        enteredDigits.Length--;
        UpdateDisplay();
    }

    // Limpa toda a entrada numérica.
    private void ClearInput()
    {
        enteredDigits.Clear();
        UpdateDisplay();
    }

    // Atualiza o texto visível e limpa mensagens de erro.
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

    // Valida a entrada e devolve o identificador ao chamador.
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

    // Cria o identificador final com prefixo P e zeros à esquerda.
    private string BuildParticipantId()
    {
        return "P" + enteredDigits.ToString().PadLeft(MinimumNumberDigits, '0');
    }

    // Desativa temporariamente a locomoção para evitar movimento durante a escrita.
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

    // Repõe a locomoção no estado em que estava antes da abertura da UI.
    private void RestoreLocomotion()
    {
        if (locomotionObject != null)
            locomotionObject.SetActive(locomotionWasActive);

        locomotionObject = null;
    }

    // Garante que existe EventSystem compatível com UI em XR.
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

    // Cria um texto TextMeshPro configurado para o painel.
    private TMP_Text CreateText(
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

    // Cria um botão com imagem arredondada e rótulo central.
    private Button CreateButton(
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
        ApplyRoundedCorners(image);

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

    // Aplica o sprite arredondado reutilizável a uma imagem.
    private void ApplyRoundedCorners(Image image)
    {
        if (image == null)
            return;

        EnsureRoundedRectangleSprite();
        image.sprite = roundedRectangleSprite;
        image.type = Image.Type.Sliced;
    }

    // Gera uma textura pequena com cantos arredondados para os elementos da UI.
    private void EnsureRoundedRectangleSprite()
    {
        if (roundedRectangleSprite != null)
            return;

        const int textureSize = 64;
        const float cornerRadius = 14f;
        Color32[] pixels = new Color32[textureSize * textureSize];

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float nearestX = Mathf.Clamp(x + 0.5f, cornerRadius, textureSize - cornerRadius);
                float nearestY = Mathf.Clamp(y + 0.5f, cornerRadius, textureSize - cornerRadius);
                float distance = Vector2.Distance(
                    new Vector2(x + 0.5f, y + 0.5f),
                    new Vector2(nearestX, nearestY));
                byte alpha = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(cornerRadius + 0.5f - distance) * 255f);

                pixels[y * textureSize + x] = new Color32(255, 255, 255, alpha);
            }
        }

        roundedRectangleTexture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false)
        {
            name = "Participant ID Rounded Rectangle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        roundedRectangleTexture.SetPixels32(pixels);
        roundedRectangleTexture.Apply();

        Vector4 border = Vector4.one * 16f;
        roundedRectangleSprite = Sprite.Create(
            roundedRectangleTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border);
        roundedRectangleSprite.name = "Participant ID Rounded Rectangle";
    }
}
