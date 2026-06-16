using System;
using System.Collections.Generic;
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
    private static readonly Dictionary<FoodType, int> savedDeliveredCounts = new Dictionary<FoodType, int>();
    private static OutdoorFoodPhaseState savedState = OutdoorFoodPhaseState.Inactive;
    private static bool hasSavedProgress = false;

    public bool IsRunning => state != OutdoorFoodPhaseState.Inactive && state != OutdoorFoodPhaseState.Completed;
    public bool HasListBeenPickedUp => state == OutdoorFoodPhaseState.DeliverFood || state == OutdoorFoodPhaseState.Completed;

    public event Action PhaseCompleted;

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();

        if (TaskManager.Instance != null)
            TaskManager.Instance.RegisterOutdoorFoodPhaseController(this);
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    public void BeginPhase(bool playerIsAlreadyInExterior = false, bool resetSavedProgress = true)
    {
        if (IsRunning)
            return;

        ResolveReferences();

        if (resetSavedProgress)
        {
            ClearSavedProgress();
            ResetProgress();
        }
        else
        {
            RestoreSavedProgress();
        }

        if (resetFoodOnBegin)
            ResetFoodCollectibles();

        if (!resetSavedProgress)
            HideAlreadyDeliveredFoodsInScene();

        if (foodListPickup != null)
            foodListPickup.ResetListPickup(playerIsAlreadyInExterior);

        SetListArrowVisible(false);
        SetTableHighlightVisible(false);

        if (!resetSavedProgress && hasSavedProgress && savedState != OutdoorFoodPhaseState.Inactive)
            ChangeState(savedState);
        else
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
            PlayOneShot(rejectedClip);
            return FoodDeliveryResult.RejectedReturnToStart;
        }

        if (state != OutdoorFoodPhaseState.DeliverFood)
            return FoodDeliveryResult.Ignored;

        FoodRequirement requirement = GetRequirement(food.FoodType);

        if (requirement == null)
        {
            PlayOneShot(rejectedClip);
            return FoodDeliveryResult.RejectedReturnToStart;
        }

        if (requirement.deliveredCount >= requirement.requiredCount)
        {
            PlayOneShot(rejectedClip);
            return FoodDeliveryResult.RejectedReturnToStart;
        }

        requirement.deliveredCount++;
        SaveProgress();
        PlayOneShot(acceptedClip);

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
        SaveProgress();
    }

    private void ChangeState(OutdoorFoodPhaseState newState)
    {
        state = newState;
        SaveProgress();

        switch (state)
        {
            case OutdoorFoodPhaseState.GoToExterior:
                SetListArrowVisible(false);
                SetTableHighlightVisible(false);
                break;

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

    private void CompletePhase()
    {
        state = OutdoorFoodPhaseState.Completed;
        SaveProgress();
        SetListArrowVisible(false);
        SetTableHighlightVisible(false);
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

    private void ClearSavedProgress()
    {
        savedDeliveredCounts.Clear();
        savedState = OutdoorFoodPhaseState.Inactive;
        hasSavedProgress = false;
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

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
