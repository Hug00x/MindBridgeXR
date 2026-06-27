using System;
using System.Collections.Generic;
using UnityEngine;

/*
 * Controla a fase exterior de recolha e entrega de alimentos.
 * Gere o estado da missão, valida a lista de alimentos pedidos, atualiza
 * indicadores diegéticos, regista métricas e preserva progresso entre cenas.
 */
public class OutdoorFoodPhaseController : MonoBehaviour
{
    // Estados principais da fase exterior.
    private enum OutdoorFoodPhaseState
    {
        Inactive,
        FindFoodList,
        DeliverFood,
        Completed
    }

    [Serializable]
    public class FoodRequirement
    {
        // Quantidade pedida de um determinado tipo de alimento.
        public FoodType foodType;
        public string displayName;
        [Min(1)] public int requiredCount = 1;
        [HideInInspector] public int deliveredCount;

        public FoodRequirement(FoodType foodType, string displayName, int requiredCount)
        {
            this.foodType = foodType;
            this.displayName = displayName;
            this.requiredCount = requiredCount;
            deliveredCount = 0;
        }
    }

    [Serializable]
    public class PlateDisplay
    {
        // Prefab e slots usados para mostrar alimentos já entregues no prato.
        public FoodType foodType;
        public GameObject visualPrefab;
        public Transform[] slots;
    }

    // Opções gerais de reinício da fase.
    [Header("Objective")]
    [SerializeField] private bool resetFoodOnBegin = true;

    // Proteção contra múltiplos colliders processarem a mesma entrega.
    [Header("Delivery Protection")]
    [Tooltip("Impede que a mesma colocação seja processada várias vezes por colliders diferentes.")]
    [SerializeField, Min(0f)] private float deliveryAttemptCooldownSeconds = 1f;

    // Referências do cenário usadas para guiar o jogador.
    [Header("Diegetic References")]
    [SerializeField] private FoodListPickup foodListPickup;
    [SerializeField] private FoodDeliveryZone deliveryZone;
    [SerializeField] private GameObject listArrowIndicator;
    [SerializeField] private GameObject tableHighlight;

    // Configuração dos objetos visuais que aparecem no prato após entregas.
    [Header("Plate Displays")]
    [SerializeField] private GameObject plateDisplaysRoot;
    [SerializeField] private List<PlateDisplay> plateDisplays = new List<PlateDisplay>();

    // Feedback sonoro para aceitar, rejeitar ou completar a fase.
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip acceptedClip;
    [SerializeField] private AudioClip rejectedClip;
    [SerializeField] private AudioClip completionClip;

    // Lista de alimentos exigidos para terminar a fase.
    [Header("Requirements")]
    [SerializeField] private List<FoodRequirement> requirements = new List<FoodRequirement>
    {
        new FoodRequirement(FoodType.Carrot, "cenoura", 3),
        new FoodRequirement(FoodType.Potato, "batata", 2),
        new FoodRequirement(FoodType.Apple, "maca", 1),
        new FoodRequirement(FoodType.Pretzel, "pretzel", 2),
        new FoodRequirement(FoodType.Mango, "manga", 1),
        new FoodRequirement(FoodType.Watermelon, "melancia", 1),
        new FoodRequirement(FoodType.Tomato, "tomate", 4)
    };

    // Estado runtime e progresso preservado entre transições de cena.
    private OutdoorFoodPhaseState state = OutdoorFoodPhaseState.Inactive;
    private readonly List<GameObject> spawnedPlateVisuals = new List<GameObject>();
    private readonly Dictionary<int, double> lastDeliveryAttemptTimes = new Dictionary<int, double>();
    private static readonly Dictionary<FoodType, int> savedDeliveredCounts = new Dictionary<FoodType, int>();
    private static OutdoorFoodPhaseState savedState = OutdoorFoodPhaseState.Inactive;
    private static bool hasSavedProgress = false;

    public bool IsRunning => state != OutdoorFoodPhaseState.Inactive && state != OutdoorFoodPhaseState.Completed;
    public bool HasListBeenPickedUp => state == OutdoorFoodPhaseState.DeliverFood || state == OutdoorFoodPhaseState.Completed;

    // Evento usado pelo TaskManager para avançar quando todos os alimentos foram entregues.
    public event Action PhaseCompleted;

    // Liga referências, esconde orientação inativa e regista o controlador no TaskManager.
    private void OnEnable()
    {
        ResolveReferences();
        HidePhaseGuidanceIfInactive();
        SubscribeEvents();

        if (TaskManager.Instance != null)
            TaskManager.Instance.RegisterOutdoorFoodPhaseController(this);
    }

    // Remove subscrições antes do controlador sair de cena.
    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    // Inicia a fase e decide se começa do zero ou restaura progresso guardado.
    public void BeginPhase(bool resetSavedProgress = true)
    {
        if (IsRunning)
            return;

        ResolveReferences();
        lastDeliveryAttemptTimes.Clear();

        if (resetSavedProgress)
        {
            ClearSavedProgress();
            ResetProgress();
        }
        else
        {
            RestoreSavedProgress();
            RebuildPlateDisplaysFromProgress();
        }

        if (resetFoodOnBegin)
            ResetFoodCollectibles();

        if (!resetSavedProgress)
            HideAlreadyDeliveredFoodsInScene();

        if (foodListPickup != null)
            foodListPickup.ResetListPickup(false);

        SetListArrowVisible(false);
        SetTableHighlightVisible(false);

        if (!resetSavedProgress && hasSavedProgress && savedState != OutdoorFoodPhaseState.Inactive)
            ChangeState(savedState);
        else
            ChangeState(OutdoorFoodPhaseState.FindFoodList);
    }

    // Valida uma tentativa de entrega e devolve a ação física que a zona deve aplicar.
    public FoodDeliveryResult TryDeliverFood(FoodCollectible food)
    {
        if (food == null || food.IsDelivered)
            return FoodDeliveryResult.Ignored;

        if (state != OutdoorFoodPhaseState.FindFoodList &&
            state != OutdoorFoodPhaseState.DeliverFood)
        {
            return FoodDeliveryResult.Ignored;
        }

        if (!TryBeginDeliveryAttempt(food))
            return FoodDeliveryResult.Ignored;

        if (state == OutdoorFoodPhaseState.FindFoodList)
        {
            PlayOneShot(rejectedClip);
            MetricsManager.Instance?.RecordFoodDeliveryAttempt(
                food,
                "rejected",
                "list_not_collected");
            return FoodDeliveryResult.RejectedReturnToStart;
        }

        FoodRequirement requirement = GetRequirement(food.FoodType);

        if (requirement == null)
        {
            PlayOneShot(rejectedClip);
            MetricsManager.Instance?.RecordFoodDeliveryAttempt(
                food,
                "rejected",
                "food_not_requested");
            return FoodDeliveryResult.RejectedReturnToStart;
        }

        if (requirement.deliveredCount >= requirement.requiredCount)
        {
            PlayOneShot(rejectedClip);
            MetricsManager.Instance?.RecordFoodDeliveryAttempt(
                food,
                "rejected",
                "required_quantity_already_complete");
            return FoodDeliveryResult.RejectedReturnToStart;
        }

        int plateSlotIndex = requirement.deliveredCount;
        requirement.deliveredCount++;
        ShowFoodOnPlate(food.FoodType, plateSlotIndex);
        SaveProgress();
        PlayOneShot(acceptedClip);
        MetricsManager.Instance?.RecordFoodDeliveryAttempt(
            food,
            "accepted",
            "accepted");

        if (IsComplete())
            CompletePhase();

        return FoodDeliveryResult.Accepted;
    }

    // Bloqueia tentativas repetidas causadas pela sobreposição de colliders.
    private bool TryBeginDeliveryAttempt(FoodCollectible food)
    {
        int foodInstanceId = food.GetInstanceID();
        double now = Time.realtimeSinceStartupAsDouble;
        double cooldown = Mathf.Max(0f, deliveryAttemptCooldownSeconds);

        if (lastDeliveryAttemptTimes.TryGetValue(foodInstanceId, out double previousAttemptTime) &&
            now - previousAttemptTime < cooldown)
        {
            return false;
        }

        lastDeliveryAttemptTimes[foodInstanceId] = now;
        return true;
    }

    // Liga eventos dos objetos de lista e zona de entrega.
    private void SubscribeEvents()
    {
        if (foodListPickup != null)
            foodListPickup.PickedUp += OnFoodListPickedUp;

        if (deliveryZone != null)
            deliveryZone.SetPhaseController(this);
    }

    // Remove eventos da lista para evitar callbacks duplicados.
    private void UnsubscribeEvents()
    {
        if (foodListPickup != null)
            foodListPickup.PickedUp -= OnFoodListPickedUp;
    }

    // Passa da procura da lista para a etapa de entregas.
    private void OnFoodListPickedUp()
    {
        if (state != OutdoorFoodPhaseState.FindFoodList)
            return;

        MetricsManager.Instance?.RecordFoodListPickedUp();
        ChangeState(OutdoorFoodPhaseState.DeliverFood);
        SaveProgress();
    }

    // Atualiza o estado e liga/desliga os indicadores visuais adequados.
    private void ChangeState(OutdoorFoodPhaseState newState)
    {
        state = newState;
        SaveProgress();

        switch (state)
        {
            case OutdoorFoodPhaseState.FindFoodList:
                SetListArrowVisible(true);
                SetTableHighlightVisible(false);
                break;

            case OutdoorFoodPhaseState.DeliverFood:
                SetListArrowVisible(false);
                SetTableHighlightVisible(true);
                break;

            case OutdoorFoodPhaseState.Completed:
                SetListArrowVisible(false);
                SetTableHighlightVisible(false);
                break;
        }
    }

    // Termina a fase, toca feedback final e avisa o fluxo principal de tarefas.
    private void CompletePhase()
    {
        state = OutdoorFoodPhaseState.Completed;
        SaveProgress();
        SetListArrowVisible(false);
        SetTableHighlightVisible(false);
        PlayOneShot(completionClip);
        PhaseCompleted?.Invoke();
    }

    // Procura referências quando não foram atribuídas manualmente no Inspector.
    private void ResolveReferences()
    {
        if (foodListPickup == null)
            foodListPickup = FindFirstObjectByType<FoodListPickup>(FindObjectsInactive.Exclude);

        if (deliveryZone == null)
            deliveryZone = FindFirstObjectByType<FoodDeliveryZone>(FindObjectsInactive.Exclude);

        if (deliveryZone != null)
            deliveryZone.SetPhaseController(this);
    }

    // Limpa contadores de entregas e objetos visuais no prato.
    private void ResetProgress()
    {
        foreach (FoodRequirement requirement in requirements)
        {
            if (requirement != null)
                requirement.deliveredCount = 0;
        }

        ClearPlateDisplays();
    }

    // Guarda o progresso em campos estáticos para sobreviver a mudanças de cena.
    private void SaveProgress()
    {
        savedDeliveredCounts.Clear();

        foreach (FoodRequirement requirement in requirements)
        {
            if (requirement == null)
                continue;

            savedDeliveredCounts[requirement.foodType] = requirement.deliveredCount;
        }

        savedState = state;
        hasSavedProgress = true;
    }

    // Recupera contadores guardados sem ultrapassar a quantidade necessária.
    private void RestoreSavedProgress()
    {
        ResetProgress();

        foreach (FoodRequirement requirement in requirements)
        {
            if (requirement == null)
                continue;

            if (savedDeliveredCounts.TryGetValue(requirement.foodType, out int deliveredCount))
                requirement.deliveredCount = Mathf.Clamp(deliveredCount, 0, requirement.requiredCount);
        }
    }

    // Reconstrói os alimentos visíveis no prato com base no progresso restaurado.
    private void RebuildPlateDisplaysFromProgress()
    {
        ClearPlateDisplays();

        foreach (FoodRequirement requirement in requirements)
        {
            if (requirement == null)
                continue;

            for (int i = 0; i < requirement.deliveredCount; i++)
                ShowFoodOnPlate(requirement.foodType, i);
        }
    }

    // Apaga o estado estático quando a fase deve começar do zero.
    private void ClearSavedProgress()
    {
        savedDeliveredCounts.Clear();
        savedState = OutdoorFoodPhaseState.Inactive;
        hasSavedProgress = false;
    }

    // Repõe todos os alimentos recolhíveis para a pose inicial.
    private void ResetFoodCollectibles()
    {
        FoodCollectible[] foods = FindObjectsByType<FoodCollectible>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (FoodCollectible food in foods)
        {
            if (food != null)
                food.ResetToStart();
        }
    }

    // Esconde no mundo alimentos que já tinham sido entregues antes da mudança de cena.
    private void HideAlreadyDeliveredFoodsInScene()
    {
        FoodCollectible[] foods = FindObjectsByType<FoodCollectible>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Dictionary<FoodType, int> remainingToHide = new Dictionary<FoodType, int>();

        foreach (FoodRequirement requirement in requirements)
        {
            if (requirement != null && requirement.deliveredCount > 0)
                remainingToHide[requirement.foodType] = requirement.deliveredCount;
        }

        foreach (FoodCollectible food in foods)
        {
            if (food == null)
                continue;

            if (!remainingToHide.TryGetValue(food.FoodType, out int remainingCount) || remainingCount <= 0)
                continue;

            food.MarkDelivered();
            remainingToHide[food.FoodType] = remainingCount - 1;
        }
    }

    // Obtém o requisito associado a um tipo de alimento.
    private FoodRequirement GetRequirement(FoodType foodType)
    {
        foreach (FoodRequirement requirement in requirements)
        {
            if (requirement != null && requirement.foodType == foodType)
                return requirement;
        }

        return null;
    }

    // Obtém a configuração visual do prato para um tipo de alimento.
    private PlateDisplay GetPlateDisplay(FoodType foodType)
    {
        foreach (PlateDisplay display in plateDisplays)
        {
            if (display != null && display.foodType == foodType)
                return display;
        }

        return null;
    }

    // Instancia o prefab do alimento no slot correto do prato.
    private void ShowFoodOnPlate(FoodType foodType, int slotIndex)
    {
        PlateDisplay display = GetPlateDisplay(foodType);

        if (display == null || display.visualPrefab == null || display.slots == null)
            return;

        if (slotIndex < 0 || slotIndex >= display.slots.Length)
        {
            return;
        }

        Transform slot = display.slots[slotIndex];

        if (slot == null)
        {
            return;
        }

        GameObject visual = Instantiate(display.visualPrefab, slot);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        spawnedPlateVisuals.Add(visual);

        if (plateDisplaysRoot != null && !plateDisplaysRoot.activeSelf)
            plateDisplaysRoot.SetActive(true);
    }

    // Remove todos os visuais de alimentos instanciados no prato.
    private void ClearPlateDisplays()
    {
        for (int i = spawnedPlateVisuals.Count - 1; i >= 0; i--)
        {
            GameObject visual = spawnedPlateVisuals[i];

            if (visual == null)
                continue;

            if (Application.isPlaying)
                Destroy(visual);
            else
                DestroyImmediate(visual);
        }

        spawnedPlateVisuals.Clear();
    }

    // Verifica se todos os requisitos já atingiram a quantidade pedida.
    private bool IsComplete()
    {
        foreach (FoodRequirement requirement in requirements)
        {
            if (requirement == null)
                continue;

            if (requirement.deliveredCount < requirement.requiredCount)
                return false;
        }

        return true;
    }

    // Liga ou desliga a orientação visual até à lista.
    private void SetListArrowVisible(bool visible)
    {
        if (listArrowIndicator != null)
            listArrowIndicator.SetActive(visible);

        if (foodListPickup != null)
            foodListPickup.SetArrowVisible(visible);
    }

    // Liga ou desliga o destaque da mesa de entrega.
    private void SetTableHighlightVisible(bool visible)
    {
        if (tableHighlight != null)
            tableHighlight.SetActive(visible);
    }

    // Garante que indicadores não ficam ativos fora do fluxo da fase.
    private void HidePhaseGuidanceIfInactive()
    {
        if (state == OutdoorFoodPhaseState.Inactive || state == OutdoorFoodPhaseState.Completed)
        {
            SetListArrowVisible(false);
            SetTableHighlightVisible(false);
        }
    }

    // Toca um som curto quando existe AudioSource configurado.
    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
