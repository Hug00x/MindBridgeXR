using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Controla a ronda do jogo de memória 3D.
 * Prepara o tabuleiro, mostra uma pré-visualização, recebe seleções de cartas,
 * avalia pares, regista métricas e emite conclusão quando todos os pares necessários
 * foram encontrados.
 */
public class MemoryMiniGame3DController : MonoBehaviour
{
    // Configuração do tabuleiro e visibilidade fora da ronda.
    [Header("Board")]
    [SerializeField] private GameObject boardRoot;
    [SerializeField] private bool hideBoardWhenNotRunning = true;

    // Lista de cartas e tempos usados durante tentativas.
    [Header("Cartas")]
    [SerializeField] private List<MemoryCard3D> cards = new List<MemoryCard3D>();
    [SerializeField] private float mismatchHideDelay = 1f;
    [SerializeField] private float previewDuration = 2f;

    // Feedback sonoro para pares corretos e incorretos.
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip correctPairClip;
    [SerializeField] private AudioClip wrongPairClip;
    [SerializeField, Range(0f, 1f)] private float audioVolume = 1f;
    [SerializeField] private float pairFeedbackAudioDelay = 0.12f;

    private MemoryCard3D firstCard;
    private MemoryCard3D secondCard;
    private bool inputLocked;
    private Coroutine roundRoutine;
    private readonly HashSet<string> requiredPairIDs = new HashSet<string>();
    private double attemptStartedRealtime;

    public bool IsRunning { get; private set; }

    public event Action RoundCompleted;

    // Resolve áudio, subscreve as cartas e deixa o tabuleiro pronto para uso.
    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        foreach (MemoryCard3D card in cards)
        {
            if (card != null)
                card.Selected += OnCardSelected;
        }

        SetBoardVisible(true);
    }

    // Remove listeners das cartas quando o controlador é destruído.
    private void OnDestroy()
    {
        foreach (MemoryCard3D card in cards)
        {
            if (card != null)
                card.Selected -= OnCardSelected;
        }
    }

    // Reinicia a ronda, revela cartas para pré-visualização e bloqueia input.
    public void BeginGame()
    {
        if (cards == null || cards.Count < 2)
        {
            return;
        }

        if (roundRoutine != null)
            StopCoroutine(roundRoutine);

        firstCard = null;
        secondCard = null;
        inputLocked = true;
        IsRunning = false;

        SetBoardVisible(true);

        RebuildRequiredPairs();

        foreach (MemoryCard3D card in cards)
        {
            if (card != null)
            {
                card.gameObject.SetActive(true);
                SetCardInteractionEnabled(card, true);
                card.ResetCard();
                card.SetInstantReveal(true);
                card.CacheBasePose();
            }
        }

        roundRoutine = StartCoroutine(BeginRoundRoutine());
    }

    // Mantém as cartas visíveis durante a pré-visualização antes de iniciar métricas.
    private IEnumerator BeginRoundRoutine()
    {
        foreach (MemoryCard3D card in cards)
        {
            if (card != null)
                card.CacheBasePose();
        }

        if (previewDuration > 0f)
            yield return new WaitForSeconds(previewDuration);

        foreach (MemoryCard3D card in cards)
        {
            if (card != null)
                card.SetInstantReveal(false);
        }

        IsRunning = true;
        inputLocked = false;
        MetricsManager.Instance?.BeginMemoryGame(requiredPairIDs.Count);
        roundRoutine = null;
    }

    // Recebe uma carta selecionada e forma pares de duas seleções.
    private void OnCardSelected(MemoryCard3D card)
    {
        if (!IsRunning || inputLocked || card == null)
            return;

        MetricsManager.Instance?.RecordMemoryCardSelected(card.MetricsId);
        card.Reveal();

        if (firstCard == null)
        {
            firstCard = card;
            attemptStartedRealtime = Time.realtimeSinceStartupAsDouble;
            return;
        }

        if (secondCard == null)
        {
            secondCard = card;
            StartCoroutine(EvaluatePairRoutine());
        }
    }

    // Compara as duas cartas, aplica feedback, atualiza métricas e testa conclusão.
    private IEnumerator EvaluatePairRoutine()
    {
        inputLocked = true;

        if (firstCard != null && secondCard != null)
        {
            bool isMatch = string.Equals(firstCard.PairID, secondCard.PairID, StringComparison.Ordinal);
            float attemptDuration = Mathf.Max(
                0f,
                (float)(Time.realtimeSinceStartupAsDouble - attemptStartedRealtime));

            MetricsManager.Instance?.RecordMemoryAttempt(
                isMatch,
                attemptDuration,
                isMatch ? firstCard.PairID : string.Empty);

            if (pairFeedbackAudioDelay > 0f)
                yield return new WaitForSeconds(pairFeedbackAudioDelay);

            if (isMatch)
            {
                firstCard.SetMatched();
                secondCard.SetMatched();
                PlayOneShot(correctPairClip);
            }
            else
            {
                PlayOneShot(wrongPairClip);
                yield return new WaitForSeconds(mismatchHideDelay);
                firstCard.Hide();
                secondCard.Hide();
            }
        }

        firstCard = null;
        secondCard = null;
        inputLocked = false;

        if (AreAllCardsMatched())
        {
            IsRunning = false;
            MetricsManager.Instance?.CompleteMemoryGame();
            RoundCompleted?.Invoke();
        }
    }

    // Verifica se todos os pares relevantes para conclusão já foram resolvidos.
    private bool AreAllCardsMatched()
    {
        int unmatchedRelevantCount = 0;
        int unmatchedNonExcludedCount = 0;
        MemoryCard3D lastUnmatched = null;

        foreach (MemoryCard3D card in cards)
        {
            if (card == null)
                continue;

            if (card.ExcludeFromCompletion)
                continue;

            if (string.IsNullOrWhiteSpace(card.PairID))
                continue;

            if (!card.IsMatched)
            {
                unmatchedNonExcludedCount++;
                lastUnmatched = card;

                if (requiredPairIDs.Contains(card.PairID))
                    unmatchedRelevantCount++;
            }
        }

        if (unmatchedRelevantCount == 0)
            return true;

        // Regra especial: uma carta "X" isolada não impede terminar a ronda.
        if (unmatchedNonExcludedCount == 1 && lastUnmatched != null && string.Equals(lastUnmatched.PairID, "X", StringComparison.Ordinal))
            return true;

        return false;
    }

    // Reconstrói o conjunto de pairID que têm pelo menos duas cartas válidas.
    private void RebuildRequiredPairs()
    {
        requiredPairIDs.Clear();

        Dictionary<string, int> pairCounts = new Dictionary<string, int>();
        foreach (MemoryCard3D card in cards)
        {
            if (card == null || card.ExcludeFromCompletion)
                continue;

            if (string.IsNullOrWhiteSpace(card.PairID))
                continue;

            if (!pairCounts.ContainsKey(card.PairID))
                pairCounts[card.PairID] = 0;

            pairCounts[card.PairID]++;
        }

        foreach (KeyValuePair<string, int> kv in pairCounts)
        {
            if (kv.Value >= 2)
                requiredPairIDs.Add(kv.Key);
        }
    }

    // Indica se uma carta conta para a condição principal de conclusão.
    private bool IsRequiredForCompletion(MemoryCard3D card)
    {
        if (card.ExcludeFromCompletion)
            return false;

        if (string.IsNullOrWhiteSpace(card.PairID))
            return false;

        return requiredPairIDs.Contains(card.PairID);
    }

    // Ativa ou desativa o tabuleiro inteiro.
    private void SetBoardVisible(bool visible)
    {
        GameObject target = boardRoot != null ? boardRoot : gameObject;
        target.SetActive(visible);
    }

    // Controla o interactable XR de cada carta durante preparação e jogo.
    private void SetCardInteractionEnabled(MemoryCard3D card, bool enabled)
    {
        if (card == null)
            return;

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable =
            card.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        if (interactable != null)
            interactable.enabled = enabled;
    }

    // Reproduz feedback sem exigir que todos os clips estejam atribuídos.
    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, audioVolume);
    }
}
