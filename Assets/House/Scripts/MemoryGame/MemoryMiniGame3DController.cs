using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MemoryMiniGame3DController : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private GameObject boardRoot;
    [SerializeField] private bool hideBoardWhenNotRunning = true;

    [Header("Cartas")]
    [SerializeField] private List<MemoryCard3D> cards = new List<MemoryCard3D>();
    [SerializeField] private float mismatchHideDelay = 1f;
    [SerializeField] private float previewDuration = 2f;

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

    public bool IsRunning { get; private set; }

    public event Action RoundCompleted;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        foreach (MemoryCard3D card in cards)
        {
            if (card != null)
                card.Selected += OnCardSelected;
        }

        if (boardRoot == gameObject)
        {
            Debug.LogWarning("MemoryMiniGame3DController: boardRoot está no mesmo objeto do controlador. Recomenda-se usar um filho dedicado para o board.");
        }

        SetBoardVisible(true);
    }

    private void OnDestroy()
    {
        foreach (MemoryCard3D card in cards)
        {
            if (card != null)
                card.Selected -= OnCardSelected;
        }
    }

    public void BeginGame()
    {
        if (cards == null || cards.Count < 2)
        {
            Debug.LogWarning("MemoryMiniGame3D: número insuficiente de cartas.");
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
        roundRoutine = null;
    }

    

    private void OnCardSelected(MemoryCard3D card)
    {
        if (!IsRunning || inputLocked || card == null)
            return;

        card.Reveal();

        if (firstCard == null)
        {
            firstCard = card;
            return;
        }

        if (secondCard == null)
        {
            secondCard = card;
            StartCoroutine(EvaluatePairRoutine());
        }
    }

    private IEnumerator EvaluatePairRoutine()
    {
        inputLocked = true;

        if (firstCard != null && secondCard != null)
        {
            bool isMatch = string.Equals(firstCard.PairID, secondCard.PairID, StringComparison.Ordinal);

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
            RoundCompleted?.Invoke();
        }
    }

    private bool AreAllCardsMatched()
    {
        // Count unmatched cards relevant for completion and keep the last unmatched reference.
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

        // If there are no unmatched cards among the required pairs, the round is complete.
        if (unmatchedRelevantCount == 0)
            return true;

        // Special rule: if exactly one non-excluded card remains unmatched and it's the special "X" card,
        // consider the round complete (player should see only the Try Again card left).
        if (unmatchedNonExcludedCount == 1 && lastUnmatched != null && string.Equals(lastUnmatched.PairID, "X", StringComparison.Ordinal))
            return true;

        return false;
    }

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

    private bool IsRequiredForCompletion(MemoryCard3D card)
    {
        if (card.ExcludeFromCompletion)
            return false;

        if (string.IsNullOrWhiteSpace(card.PairID))
            return false;

        return requiredPairIDs.Contains(card.PairID);
    }

    

    private void SetBoardVisible(bool visible)
    {
        GameObject target = boardRoot != null ? boardRoot : gameObject;
        target.SetActive(visible);
    }

    private void SetCardInteractionEnabled(MemoryCard3D card, bool enabled)
    {
        if (card == null)
            return;

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable =
            card.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        if (interactable != null)
            interactable.enabled = enabled;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, audioVolume);
    }
}
