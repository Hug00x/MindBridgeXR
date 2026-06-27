/*
 * Resultado devolvido pela zona de entrega de alimentos.
 * Permite distinguir entregas aceites, objetos ignorados e alimentos que
 * devem regressar à posição inicial depois de uma rejeição.
 */
public enum FoodDeliveryResult
{
    // A zona não deve fazer nenhuma ação sobre este alimento.
    Ignored,

    // A entrega foi aceite e o alimento pode ser marcado como entregue.
    Accepted,

    // A entrega foi rejeitada e o alimento deve voltar ao ponto de origem.
    RejectedReturnToStart
}
