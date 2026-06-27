using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Zona de entrega colocada sobre a mesa exterior.
 * Observa alimentos que entram no trigger, espera que sejam largados e pede ao
 * controlador da fase para aceitar, rejeitar ou ignorar cada tentativa.
 */
[RequireComponent(typeof(Collider))]
public class FoodDeliveryZone : MonoBehaviour
{
    // Ligações à fase e parâmetros de comportamento da zona.
    [SerializeField] private OutdoorFoodPhaseController phaseController;
    [SerializeField] private bool autoFindPhaseController = true;
    [SerializeField] private bool forceColliderAsTrigger = true;
    [SerializeField] private float rejectedReturnDelay = 0.35f;

    // Alimentos atualmente dentro do trigger para evitar processamentos repetidos.
    private readonly HashSet<FoodCollectible> foodsInside = new HashSet<FoodCollectible>();
    private Collider zoneCollider;

    // Prepara o collider como trigger e tenta localizar o controlador da fase.
    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();

        if (zoneCollider != null && forceColliderAsTrigger)
            zoneCollider.isTrigger = true;

        ResolvePhaseController();
    }

    // Remove subscrições dos alimentos que ficaram dentro da zona.
    private void OnDisable()
    {
        foreach (FoodCollectible food in foodsInside)
        {
            if (food != null)
                food.Released -= OnFoodReleased;
        }

        foodsInside.Clear();
    }

    // Permite ao controlador da fase registar explicitamente esta zona.
    public void SetPhaseController(OutdoorFoodPhaseController controller)
    {
        phaseController = controller;
    }

    // Começa a acompanhar o alimento quando entra na zona de entrega.
    private void OnTriggerEnter(Collider other)
    {
        FoodCollectible food = other.GetComponentInParent<FoodCollectible>();
        if (food == null || !foodsInside.Add(food))
            return;

        food.Released += OnFoodReleased;
        TryResolveFood(food);
    }

    // Deixa de acompanhar alimentos que saem da área antes de serem largados.
    private void OnTriggerExit(Collider other)
    {
        FoodCollectible food = other.GetComponentInParent<FoodCollectible>();
        if (food == null || !foodsInside.Remove(food))
            return;

        food.Released -= OnFoodReleased;
    }

    // Reavalia a entrega no momento em que o jogador larga o alimento.
    private void OnFoodReleased(FoodCollectible food)
    {
        if (food == null || !foodsInside.Contains(food))
            return;

        TryResolveFood(food);
    }

    // Pede a decisão ao controlador e executa a consequência física no alimento.
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

    // Dá um pequeno intervalo visual antes de devolver alimentos rejeitados.
    private IEnumerator ReturnFoodAfterDelay(FoodCollectible food)
    {
        if (rejectedReturnDelay > 0f)
            yield return new WaitForSeconds(rejectedReturnDelay);

        if (food != null && !food.IsHeld && !food.IsDelivered)
            food.ReturnToStart();
    }

    // Localiza automaticamente o controlador quando a referência não foi atribuída.
    private void ResolvePhaseController()
    {
        if (phaseController != null || !autoFindPhaseController)
            return;

        phaseController = FindFirstObjectByType<OutdoorFoodPhaseController>(FindObjectsInactive.Exclude);
    }
}
