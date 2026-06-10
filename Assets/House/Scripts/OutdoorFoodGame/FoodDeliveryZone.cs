using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FoodDeliveryZone : MonoBehaviour
{
    [SerializeField] private OutdoorFoodPhaseController phaseController;
    [SerializeField] private bool autoFindPhaseController = true;
    [SerializeField] private bool forceColliderAsTrigger = true;
    [SerializeField] private float rejectedReturnDelay = 0.35f;

    private readonly HashSet<FoodCollectible> foodsInside = new HashSet<FoodCollectible>();
    private Collider zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();

        if (zoneCollider != null && forceColliderAsTrigger)
            zoneCollider.isTrigger = true;

        ResolvePhaseController();
    }

    private void OnDisable()
    {
        foreach (FoodCollectible food in foodsInside)
        {
            if (food != null)
                food.Released -= OnFoodReleased;
        }

        foodsInside.Clear();
    }

    public void SetPhaseController(OutdoorFoodPhaseController controller)
    {
        phaseController = controller;
    }

    private void OnTriggerEnter(Collider other)
    {
        FoodCollectible food = other.GetComponentInParent<FoodCollectible>();
        if (food == null || !foodsInside.Add(food))
            return;

        food.Released += OnFoodReleased;
        TryResolveFood(food);
    }

    private void OnTriggerExit(Collider other)
    {
        FoodCollectible food = other.GetComponentInParent<FoodCollectible>();
        if (food == null || !foodsInside.Remove(food))
            return;

        food.Released -= OnFoodReleased;
    }

    private void OnFoodReleased(FoodCollectible food)
    {
        if (food == null || !foodsInside.Contains(food))
            return;

        TryResolveFood(food);
    }

    private void TryResolveFood(FoodCollectible food)
    {
        if (food == null || food.IsHeld || food.IsDelivered)
            return;

        ResolvePhaseController();

        if (phaseController == null)
            return;

        FoodDeliveryResult result = phaseController.TryDeliverFood(food);

        if (result == FoodDeliveryResult.Accepted)
        {
            foodsInside.Remove(food);
            food.Released -= OnFoodReleased;
            food.MarkDelivered();
        }
        else if (result == FoodDeliveryResult.RejectedReturnToStart)
        {
            foodsInside.Remove(food);
            food.Released -= OnFoodReleased;
            StartCoroutine(ReturnFoodAfterDelay(food));
        }
    }

    private IEnumerator ReturnFoodAfterDelay(FoodCollectible food)
    {
        if (rejectedReturnDelay > 0f)
            yield return new WaitForSeconds(rejectedReturnDelay);

        if (food != null && !food.IsHeld && !food.IsDelivered)
            food.ReturnToStart();
    }

    private void ResolvePhaseController()
    {
        if (phaseController != null || !autoFindPhaseController)
            return;

        phaseController = FindFirstObjectByType<OutdoorFoodPhaseController>(FindObjectsInactive.Exclude);
    }
}
