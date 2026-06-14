using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class OutdoorFoodPhaseController : MonoBehaviour
{
    private enum OutdoorFoodPhaseState
    {
        Inactive,
        GoToExterior,
        FindFoodList,
        DeliverFood,
        Completed
    }

    [Serializable]
    public class FoodRequirement
    {
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

    [Header("Objective")]
    [SerializeField] private string exteriorRoomID = "exterior";
    [SerializeField] private bool resetFoodOnBegin = true;

    [Header("Diegetic References")]
    [SerializeField] private FoodListPickup foodListPickup;
    [SerializeField] private FoodDeliveryZone deliveryZone;
    [SerializeField] private GameObject listArrowIndicator;
    [SerializeField] private GameObject tableHighlight;

    [Header("Task Text")]
    [SerializeField] private TMP_Text taskText;
    [SerializeField] private TMP_Text centerMessageText;
    [SerializeField] private float centerMessageDuration = 2.5f;

    [Header("Messages")]
    [SerializeField] private string goToExteriorTask = "Fase 4: Vai ate ao exterior.";
    [SerializeField] private string findListTask = "Fase 4: Pega na lista de alimentos.";
    [SerializeField] private string deliverFoodTask = "Fase 4: Coloca na mesa os alimentos pedidos.";
    [SerializeField] private string completionTask = "Fase 4 concluida";
    [SerializeField] private string enteredExteriorMessage = "Prepara a mesa com os alimentos da lista.";
    [SerializeField] private string findListMessage = "Pega na lista para veres o pedido.";
    [SerializeField] private string listPickedMessage = "Boa. Agora coloca os alimentos pedidos na mesa.";
    [SerializeField] private string completionMessage = "Excelente! Preparaste a mesa.";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip acceptedClip;
    [SerializeField] private AudioClip rejectedClip;
    [SerializeField] private AudioClip completionClip;

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

    private OutdoorFoodPhaseState state = OutdoorFoodPhaseState.Inactive;
    private Coroutine centerMessageRoutine;

    public bool IsRunning => state != OutdoorFoodPhaseState.Inactive && state != OutdoorFoodPhaseState.Completed;
    public bool HasListBeenPickedUp => state == OutdoorFoodPhaseState.DeliverFood || state == OutdoorFoodPhaseState.Completed;

    public event Action PhaseCompleted;

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
        HideCenterMessage();

        if (TaskManager.Instance != null)
            TaskManager.Instance.RegisterOutdoorFoodPhaseController(this);
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    public void BeginPhase(bool playerIsAlreadyInExterior = false)
    {
        if (IsRunning)
            return;

        ResolveReferences();
        ResetProgress();

        if (resetFoodOnBegin)
            ResetFoodCollectibles();

        if (foodListPickup != null)
            foodListPickup.ResetListPickup(playerIsAlreadyInExterior);

        SetListArrowVisible(false);
        SetTableHighlightVisible(false);

        ChangeState(playerIsAlreadyInExterior ? OutdoorFoodPhaseState.FindFoodList : OutdoorFoodPhaseState.GoToExterior);
    }

    public void NotifyPlayerEnteredRoom(string roomID)
    {
        if (state != OutdoorFoodPhaseState.GoToExterior)
            return;

        if (!string.Equals(roomID, exteriorRoomID, StringComparison.OrdinalIgnoreCase))
            return;

        ChangeState(OutdoorFoodPhaseState.FindFoodList);
    }

    public FoodDeliveryResult TryDeliverFood(FoodCollectible food)
    {
        if (food == null || food.IsDelivered)
            return FoodDeliveryResult.Ignored;

        if (state == OutdoorFoodPhaseState.GoToExterior || state == OutdoorFoodPhaseState.FindFoodList)
        {
            ShowCenterMessage("Pega primeiro na lista.");
            PlayOneShot(rejectedClip);
            return FoodDeliveryResult.RejectedReturnToStart;
        }

        if (state != OutdoorFoodPhaseState.DeliverFood)
            return FoodDeliveryResult.Ignored;

        FoodRequirement requirement = GetRequirement(food.FoodType);

        if (requirement == null)
        {
            ShowCenterMessage("Esse alimento nao esta na lista.");
            PlayOneShot(rejectedClip);
            return FoodDeliveryResult.RejectedReturnToStart;
        }

        if (requirement.deliveredCount >= requirement.requiredCount)
        {
            ShowCenterMessage("Ja tens " + requirement.displayName + " suficientes.");
            PlayOneShot(rejectedClip);
            return FoodDeliveryResult.RejectedReturnToStart;
        }

        requirement.deliveredCount++;
        ShowCenterMessage(requirement.displayName + " adicionada (" +
                          requirement.deliveredCount + "/" + requirement.requiredCount + ")");
        PlayOneShot(acceptedClip);
        SetTask(BuildProgressTask());

        if (IsComplete())
            CompletePhase();

        return FoodDeliveryResult.Accepted;
    }

    private void SubscribeEvents()
    {
        if (foodListPickup != null)
            foodListPickup.PickedUp += OnFoodListPickedUp;

        if (deliveryZone != null)
            deliveryZone.SetPhaseController(this);
    }

    private void UnsubscribeEvents()
    {
        if (foodListPickup != null)
            foodListPickup.PickedUp -= OnFoodListPickedUp;
    }

    private void OnFoodListPickedUp()
    {
        if (state != OutdoorFoodPhaseState.FindFoodList)
            return;

        ChangeState(OutdoorFoodPhaseState.DeliverFood);
    }

    private void ChangeState(OutdoorFoodPhaseState newState)
    {
        state = newState;

        switch (state)
        {
            case OutdoorFoodPhaseState.GoToExterior:
                SetTask(goToExteriorTask);
                SetListArrowVisible(false);
                SetTableHighlightVisible(false);
                break;

            case OutdoorFoodPhaseState.FindFoodList:
                SetTask(findListTask);
                SetListArrowVisible(true);
                SetTableHighlightVisible(false);
                ShowCenterMessage(enteredExteriorMessage + " " + findListMessage);
                break;

            case OutdoorFoodPhaseState.DeliverFood:
                SetTask(BuildProgressTask());
                SetListArrowVisible(false);
                SetTableHighlightVisible(true);
                ShowCenterMessage(listPickedMessage);
                break;

            case OutdoorFoodPhaseState.Completed:
                SetTask(completionTask);
                SetListArrowVisible(false);
                SetTableHighlightVisible(false);
                break;
        }
    }

    private void CompletePhase()
    {
        state = OutdoorFoodPhaseState.Completed;
        SetTask(completionTask);
        SetListArrowVisible(false);
        SetTableHighlightVisible(false);
        ShowCenterMessage(completionMessage);
        PlayOneShot(completionClip);
        PhaseCompleted?.Invoke();
    }

    private void ResolveReferences()
    {
        if (foodListPickup == null)
            foodListPickup = FindFirstObjectByType<FoodListPickup>(FindObjectsInactive.Exclude);

        if (deliveryZone == null)
            deliveryZone = FindFirstObjectByType<FoodDeliveryZone>(FindObjectsInactive.Exclude);

        if (deliveryZone != null)
            deliveryZone.SetPhaseController(this);
    }

    private void ResetProgress()
    {
        foreach (FoodRequirement requirement in requirements)
        {
            if (requirement != null)
                requirement.deliveredCount = 0;
        }
    }

    private void ResetFoodCollectibles()
    {
        FoodCollectible[] foods = FindObjectsByType<FoodCollectible>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (FoodCollectible food in foods)
        {
            if (food != null)
                food.ResetToStart();
        }
    }

    private FoodRequirement GetRequirement(FoodType foodType)
    {
        foreach (FoodRequirement requirement in requirements)
        {
            if (requirement != null && requirement.foodType == foodType)
                return requirement;
        }

        return null;
    }

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

    private int GetDeliveredTotal()
    {
        int total = 0;

        foreach (FoodRequirement requirement in requirements)
        {
            if (requirement != null)
                total += requirement.deliveredCount;
        }

        return total;
    }

    private int GetRequiredTotal()
    {
        int total = 0;

        foreach (FoodRequirement requirement in requirements)
        {
            if (requirement != null)
                total += requirement.requiredCount;
        }

        return total;
    }

    private string BuildProgressTask()
    {
        return deliverFoodTask + " (" + GetDeliveredTotal() + "/" + GetRequiredTotal() + ")";
    }

    private void SetTask(string message)
    {
        if (taskText != null)
            taskText.text = message;
    }

    private void SetListArrowVisible(bool visible)
    {
        if (listArrowIndicator != null)
            listArrowIndicator.SetActive(visible);

        if (foodListPickup != null)
            foodListPickup.SetArrowVisible(visible);
    }

    private void SetTableHighlightVisible(bool visible)
    {
        if (tableHighlight != null)
            tableHighlight.SetActive(visible);
    }

    private void ShowCenterMessage(string message)
    {
        if (centerMessageText == null || string.IsNullOrWhiteSpace(message))
            return;

        if (centerMessageRoutine != null)
            StopCoroutine(centerMessageRoutine);

        centerMessageRoutine = StartCoroutine(CenterMessageRoutine(message));
    }

    private IEnumerator CenterMessageRoutine(string message)
    {
        centerMessageText.gameObject.SetActive(true);
        centerMessageText.text = message;

        yield return new WaitForSeconds(centerMessageDuration);

        HideCenterMessage();
        centerMessageRoutine = null;
    }

    private void HideCenterMessage()
    {
        if (centerMessageText == null)
            return;

        centerMessageText.text = string.Empty;
        centerMessageText.gameObject.SetActive(false);
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
