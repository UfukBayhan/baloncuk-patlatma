using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BubblePopMathGame : MonoBehaviour
{
    private StemGameManager stem;
    public BubbleGameMode gameMode;
    private TextMeshProUGUI yonergeText;
    private GameObject WinSFX;
    private GameObject WinSFXFinal;
    private GameObject WrongSFX;
    private GameObject SoundFX;

    [Header("Game Objects")]
    public GameObject bubbleContainer;
    public GameObject bubblePrefab;

    [Header("Game Settings")]
    public int levelIndex = 0;
    public int targetPopCount = 5;
    public float spawnInterval = 2.0f;
    public float bubbleSpeed = 1.2f;
    public int maxBubbles = 8;
    public Vector2 spawnAreaWidth = new Vector2(-400f, 400f);
    private bool isRandomMode = false;

    public Vector2Int numberRangeXY = new Vector2Int(1, 50);
    public int minNumber = 1;
    public int maxNumber = 50;

    [Header("Level State")]
    private int currentTargetNumber = 0;
    public int correctPops = 0;
    private bool levelTransitioning = false;

    private BubbleGameMode currentRandomMode; // isRandom=true için aktif mod
    private bool isGreaterThanMode = true; // Hangi mod aktif (true=büyük, false=küçük)

    public List<int> selectedNumbers = new List<int>();
    public List<GameObject> selectedBubbles = new List<GameObject>();
    public Queue<int> recentValuesAddition = new Queue<int>(5);
    private int currentSum = 0;

    private Queue<int> recentValuesGeneral = new Queue<int>(8);

    private Queue<GameObject> bubblePool = new Queue<GameObject>();
    private List<GameObject> activeBubbles = new List<GameObject>();
    private const int INITIAL_BUBBLE_POOL = 20;
    private const int MAX_POOL_SIZE = 40;

    private Dictionary<GameObject, BubbleData> bubbleDataMap =
        new Dictionary<GameObject, BubbleData>();
    private StringBuilder stringBuilder = new StringBuilder(200);
    private System.Random sharedRandom = new System.Random();

    private Coroutine spawnCoroutine;
    private float despawnHeight;

    void Start()
    {
        stem = FindAnyObjectByType<StemGameManager>();
        if (stem != null)
        {
            InitializeComponents();
        }

        InitializeObjectPools();
        InitializeRandomMode();
        ApplyDifficultyForLevel(levelIndex);
        StartLevel();
    }

    private void OnDestroy()
    {
        CleanupPools();
    }

    private void InitializeComponents()
    {
        yonergeText = stem.yonergeText;
        WinSFX = stem.WinSFX;
        WrongSFX = stem.WrongSFX;
        WinSFXFinal = stem.WinSFXFinal;
        gameMode = stem.bubbleGameMode;
        levelIndex = stem.levelIndex;
        isRandomMode = stem.isRandom;

        if (stem.numberRangeXY != Vector2.zero)
        {
            numberRangeXY = stem.numberRangeXY;
            minNumber = Mathf.Max(1, (int)numberRangeXY.x);
            maxNumber = Mathf.Max(minNumber, (int)numberRangeXY.y);
        }

        if (stem.trueObjects != null && stem.trueObjects.Length > 0 && stem.trueObjects[0] != null)
        {
            bubbleContainer = stem.trueObjects[0];
            despawnHeight = ((RectTransform)bubbleContainer.transform).rect.height / 2f + 100f;
        }
        else
        {
            Debug.LogError("[BubblePopMathGame] trueObjects[0] (BubbleContainer) is missing!");
        }

        if (stem.etcPrefab != null && stem.etcPrefab.Length > 0)
        {
            bubblePrefab = stem.etcPrefab[0];
        }
        else
        {
            Debug.LogError("[BubblePopMathGame] etcPrefab missing! Need bubblePrefab");
        }

        if (stem.step > 0)
        {
            targetPopCount = stem.step;
        }
    }

    private void InitializeRandomMode()
    {
        if (
            isRandomMode
            && (gameMode == BubbleGameMode.GreaterThan || gameMode == BubbleGameMode.LessThan)
        )
        {
            // Random mod aktifse, başlangıçta random bir mod seç
            currentRandomMode =
                (sharedRandom.Next(0, 2) == 0)
                    ? BubbleGameMode.GreaterThan
                    : BubbleGameMode.LessThan;

            isGreaterThanMode = (currentRandomMode == BubbleGameMode.GreaterThan);

            Debug.Log($"[RandomMode] Initial mode: {currentRandomMode}");
        }
    }

    private void SwitchRandomMode()
    {
        if (
            isRandomMode
            && (gameMode == BubbleGameMode.GreaterThan || gameMode == BubbleGameMode.LessThan)
        )
        {
            // Her level bitiminde random olarak değiştir
            currentRandomMode =
                (sharedRandom.Next(0, 2) == 0)
                    ? BubbleGameMode.GreaterThan
                    : BubbleGameMode.LessThan;

            isGreaterThanMode = (currentRandomMode == BubbleGameMode.GreaterThan);

            Debug.Log($"[RandomMode] Switched to: {currentRandomMode}");
        }
    }

    private void InitializeObjectPools()
    {
        if (bubblePrefab == null || bubbleContainer == null)
        {
            Debug.LogError("[BubblePopMathGame] Bubble prefab or container is missing!");
            return;
        }

        for (int i = 0; i < INITIAL_BUBBLE_POOL; i++)
        {
            GameObject bubble = Instantiate(bubblePrefab, bubbleContainer.transform);
            bubble.name = $"PooledBubble_{i}";

            Transform explodingChild = bubble.transform.Find("Exploding");
            if (explodingChild != null)
                explodingChild.gameObject.SetActive(false);

            bubble.SetActive(false);

            Button btn = bubble.GetComponent<Button>();
            if (btn == null)
                btn = bubble.AddComponent<Button>();

            bubblePool.Enqueue(bubble);
        }

        Debug.Log($"[BubblePool] Initialized: {INITIAL_BUBBLE_POOL} bubbles");
    }

    private GameObject GetBubbleFromPool()
    {
        GameObject bubble;

        if (bubblePool.Count > 0)
        {
            bubble = bubblePool.Dequeue();
        }
        else if (activeBubbles.Count < MAX_POOL_SIZE)
        {
            bubble = Instantiate(bubblePrefab, bubbleContainer.transform);
            bubble.name = $"ExtraBubble_{activeBubbles.Count}";

            Button btn = bubble.GetComponent<Button>();
            if (btn == null)
                btn = bubble.AddComponent<Button>();
        }
        else
        {
            Debug.LogWarning("[BubblePool] Max pool size reached!");
            return null;
        }

        RectTransform rect = bubble.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one;
            rect.rotation = Quaternion.identity;
        }

        Transform exploding = bubble.transform.Find("Exploding");
        if (exploding != null && exploding.gameObject.activeSelf)
        {
            Debug.LogWarning(
                $"[BubblePool] {bubble.name} Exploding was still active! Disabling..."
            );
            exploding.gameObject.SetActive(false);
        }

        bubble.SetActive(true);
        activeBubbles.Add(bubble);
        return bubble;
    }

    private void ReturnBubbleToPool(GameObject bubble)
    {
        if (bubble == null)
            return;

        if (!activeBubbles.Contains(bubble))
        {
            Debug.LogWarning($"[BubblePool] {bubble.name} already returned to pool!");
            return;
        }

        Transform explodingChild = bubble.transform.Find("Exploding");
        if (explodingChild != null)
        {
            explodingChild.gameObject.SetActive(false);
        }

        Button btn = bubble.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.interactable = true;
        }

        Image bubbleImg = bubble.GetComponent<Image>();
        if (bubbleImg != null)
        {
            Color c = bubbleImg.color;
            c.a = 1f;
            bubbleImg.color = c;
            bubbleImg.enabled = true;
        }

        TextMeshProUGUI text = bubble.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.enabled = true;

        bubble.SetActive(false);
        activeBubbles.Remove(bubble);
        bubbleDataMap.Remove(bubble);

        if (bubblePool.Count < MAX_POOL_SIZE)
            bubblePool.Enqueue(bubble);
        else
            Destroy(bubble);
    }

    private void CleanupPools()
    {
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);

        foreach (var bubble in activeBubbles)
        {
            if (bubble != null)
                Destroy(bubble);
        }
        activeBubbles.Clear();

        while (bubblePool.Count > 0)
        {
            var bubble = bubblePool.Dequeue();
            if (bubble != null)
                Destroy(bubble);
        }

        bubbleDataMap.Clear();
    }

    private void StartLevel()
    {
        ClearAllBubbles();

        selectedNumbers.Clear();
        selectedBubbles.Clear();
        currentSum = 0;

        recentValuesAddition.Clear();
        recentValuesGeneral.Clear();

        currentTargetNumber = GenerateTargetNumber();

        UpdateYonergeText();

        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);

        spawnCoroutine = StartCoroutine(SpawnBubblesRoutine());
    }

    private void ClearAllBubbles()
    {
        var bubblesToClear = new List<GameObject>(activeBubbles);
        foreach (var bubble in bubblesToClear)
        {
            if (bubble != null)
                ReturnBubbleToPool(bubble);
        }
    }

    private IEnumerator SpawnBubblesRoutine()
    {
        while (true)
        {
            if (activeBubbles.Count < maxBubbles)
            {
                SpawnBubble();
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnBubble()
    {
        GameObject bubble = GetBubbleFromPool();
        if (bubble == null)
            return;

        float randomX = Random.Range(spawnAreaWidth.x, spawnAreaWidth.y);
        RectTransform rect = bubble.GetComponent<RectTransform>();

        float startY = -(despawnHeight + 50f);
        rect.anchoredPosition = new Vector2(randomX, startY);

        int value = GenerateBubbleValue();

        TextMeshProUGUI text = bubble.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.text = value.ToString();

        BubbleData data = new BubbleData
        {
            value = value,
            rect = rect,
            text = text,
        };
        bubbleDataMap[bubble] = data;

        Button btn = bubble.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            GameObject captured = bubble;
            btn.onClick.AddListener(() => OnBubbleClicked(captured));
        }

        var movable = bubble.GetComponent<MovableObject>();
        if (movable != null)
        {
            movable.direction = MoveDirection.Up;
            movable.speed = bubbleSpeed;
            movable.OnDespawn = HandleBubbleOutOfScreen;
        }
    }

    private void HandleBubbleOutOfScreen(MovableObject movable)
    {
        if (movable == null)
            return;
        ReturnBubbleToPool(movable.gameObject);
    }

    private int GenerateBubbleValue()
    {
        int correctChance = GetCorrectBubbleChance();

        BubbleGameMode activeMode = GetActiveMode();

        switch (activeMode)
        {
            case BubbleGameMode.AdditionBubbles:
                return GenerateAdditionValue();

            case BubbleGameMode.MultiplicationPop:
                return GenerateMultiplicationValue(correctChance);

            case BubbleGameMode.FactorBubbles:
                return GenerateFactorValue(correctChance);

            case BubbleGameMode.PrimeBubbles:
                return GeneratePrimeValue(correctChance);

            case BubbleGameMode.GreaterThan:
                return GenerateGreaterThanValue(correctChance);

            case BubbleGameMode.LessThan:
                return GenerateLessThanValue(correctChance);

            default:
                return sharedRandom.Next(minNumber, maxNumber + 1);
        }
    }

    // Rastgele karşılaştırma modunda geçerli hedefi kullanır.
    private BubbleGameMode GetActiveMode()
    {
        if (
            isRandomMode
            && (gameMode == BubbleGameMode.GreaterThan || gameMode == BubbleGameMode.LessThan)
        )
        {
            return currentRandomMode;
        }
        return gameMode;
    }

    private int GenerateGreaterThanValue(int correctChance)
    {
        bool shouldBeGreater = sharedRandom.Next(0, 100) < correctChance;
        int generatedValue;
        int maxAttempts = 20;
        int attempt = 0;

        do
        {
            if (shouldBeGreater)
            {
                // Hedef sayıdan büyük bir değer üret
                int lowerBound = currentTargetNumber + 1;
                int upperBound = maxNumber;

                if (lowerBound > upperBound)
                {
                    // Eğer hedef sayı max'a çok yakınsa, biraz daha geniş aralık kullan
                    generatedValue = currentTargetNumber + sharedRandom.Next(1, 6);
                }
                else
                {
                    generatedValue = sharedRandom.Next(lowerBound, upperBound + 1);
                }
            }
            else
            {
                // Hedef sayıdan küçük veya eşit bir değer üret
                int upperBound = currentTargetNumber;
                int lowerBound = minNumber;

                generatedValue = sharedRandom.Next(lowerBound, upperBound + 1);
            }

            attempt++;
        } while (
            (generatedValue == currentTargetNumber || recentValuesGeneral.Contains(generatedValue))
            && attempt < maxAttempts
        );

        recentValuesGeneral.Enqueue(generatedValue);
        if (recentValuesGeneral.Count > 8)
            recentValuesGeneral.Dequeue();

        return generatedValue;
    }

    private int GenerateLessThanValue(int correctChance)
    {
        bool shouldBeLess = sharedRandom.Next(0, 100) < correctChance;
        int generatedValue;
        int maxAttempts = 20;
        int attempt = 0;

        do
        {
            if (shouldBeLess)
            {
                // Hedef sayıdan küçük bir değer üret
                int upperBound = currentTargetNumber - 1;
                int lowerBound = minNumber;

                if (upperBound < lowerBound)
                {
                    // Eğer hedef sayı min'e çok yakınsa, biraz daha geniş aralık kullan
                    generatedValue = Mathf.Max(
                        minNumber,
                        currentTargetNumber - sharedRandom.Next(1, 6)
                    );
                }
                else
                {
                    generatedValue = sharedRandom.Next(lowerBound, upperBound + 1);
                }
            }
            else
            {
                // Hedef sayıdan büyük veya eşit bir değer üret
                int lowerBound = currentTargetNumber;
                int upperBound = maxNumber;

                generatedValue = sharedRandom.Next(lowerBound, upperBound + 1);
            }

            attempt++;
        } while (
            (generatedValue == currentTargetNumber || recentValuesGeneral.Contains(generatedValue))
            && attempt < maxAttempts
        );

        recentValuesGeneral.Enqueue(generatedValue);
        if (recentValuesGeneral.Count > 8)
            recentValuesGeneral.Dequeue();

        return generatedValue;
    }

    private int GenerateAdditionValue()
    {
        int remaining = currentTargetNumber - currentSum;
        int generatedValue;

        if (selectedNumbers.Count > 0 && remaining > 0)
        {
            int roll = sharedRandom.Next(0, 100);

            if (roll < 40)
            {
                generatedValue = remaining;
            }
            else
            {
                int lowerBound = Mathf.Max(minNumber, 1);
                int upperBound = Mathf.Min(currentTargetNumber - 1, maxNumber);

                if (remaining <= 2)
                {
                    generatedValue = sharedRandom.Next(lowerBound, upperBound + 1);
                    if (generatedValue == remaining)
                        generatedValue = Mathf.Clamp(generatedValue + 1, lowerBound, upperBound);
                }
                else
                {
                    int maxBelow = Mathf.Max(minNumber, remaining - 1);
                    generatedValue = sharedRandom.Next(lowerBound, maxBelow + 1);
                }
            }
        }
        else
        {
            int maxFirstValue = Mathf.Max(2, (currentTargetNumber * 2) / 3);
            generatedValue = sharedRandom.Next(
                minNumber,
                Mathf.Min(maxFirstValue + 1, currentTargetNumber + 1)
            );
        }

        int attempt = 0;
        while (recentValuesAddition.Contains(generatedValue) && attempt < 5)
        {
            generatedValue = sharedRandom.Next(
                minNumber,
                Mathf.Min(currentTargetNumber, maxNumber + 1)
            );
            attempt++;
        }

        recentValuesAddition.Enqueue(generatedValue);
        if (recentValuesAddition.Count > 5)
            recentValuesAddition.Dequeue();

        return generatedValue;
    }

    private int GenerateMultiplicationValue(int correctChance)
    {
        bool isMultiple = sharedRandom.Next(0, 100) < correctChance;
        int generatedValue;
        int maxAttempts = 20;
        int attempt = 0;

        do
        {
            if (isMultiple && currentTargetNumber > 0)
            {
                int maxMult = Mathf.Max(1, maxNumber / currentTargetNumber);
                int mult = sharedRandom.Next(1, Mathf.Min(20, maxMult + 1));
                generatedValue = currentTargetNumber * mult;
            }
            else
            {
                generatedValue = sharedRandom.Next(minNumber, maxNumber + 1);
                if (currentTargetNumber > 0 && generatedValue % currentTargetNumber == 0)
                {
                    generatedValue =
                        (generatedValue + 1 > maxNumber) ? generatedValue - 1 : generatedValue + 1;
                }
            }
            attempt++;
        } while (recentValuesGeneral.Contains(generatedValue) && attempt < maxAttempts);

        recentValuesGeneral.Enqueue(generatedValue);
        if (recentValuesGeneral.Count > 8)
            recentValuesGeneral.Dequeue();

        return generatedValue;
    }

    private int GenerateFactorValue(int correctChance)
    {
        bool isFactor = sharedRandom.Next(0, 100) < correctChance;
        int generatedValue = 1;
        int maxAttempts = 30;
        int attempt = 0;

        if (isFactor)
        {
            // Doğru seçenekler hedef sayının çarpanlarından seçilir.
            List<int> factors = GetFactors(currentTargetNumber);

            if (factors.Count > 0)
            {
                do
                {
                    generatedValue = factors[sharedRandom.Next(0, factors.Count)];
                    attempt++;
                } while (recentValuesGeneral.Contains(generatedValue) && attempt < maxAttempts);
            }
            else
            {
                generatedValue = 1; // fallback
            }
        }
        else
        {
            // Yanlış seçenekler hedefe yakın, çarpan olmayan sayılardan seçilir.
            int offset = sharedRandom.Next(10, 21); // hedefin ±10–20 çevresi
            bool goAbove = sharedRandom.Next(0, 2) == 0; // yukarı mı aşağı mı?

            int lowerBound = Mathf.Max(2, currentTargetNumber - offset);
            int upperBound = currentTargetNumber + offset;

            // güvenli clamp (OutOfRange hatasını önler)
            if (upperBound < lowerBound)
                upperBound = lowerBound + 1;

            attempt = 0;
            do
            {
                generatedValue = sharedRandom.Next(lowerBound, upperBound + 1);
                attempt++;
            } while (
                (
                    currentTargetNumber % generatedValue == 0 // çarpan olmasın
                    || IsPrime(generatedValue) // asal olmasın
                    || recentValuesGeneral.Contains(generatedValue)
                )
                && attempt < maxAttempts
            );

            if (attempt >= maxAttempts)
            {
                // fallback: hedefin yakınında sabit bir sayı üret
                generatedValue = Mathf.Clamp(
                    currentTargetNumber + sharedRandom.Next(3, 8),
                    2,
                    maxNumber
                );
            }
        }

        // Tekrarları azalt
        recentValuesGeneral.Enqueue(generatedValue);
        if (recentValuesGeneral.Count > 8)
            recentValuesGeneral.Dequeue();

        return generatedValue;
    }

    private int GeneratePrimeValue(int correctChance)
    {
        bool shouldBePrime = sharedRandom.Next(0, 100) < correctChance;
        int generatedValue;
        int maxAttempts = 20;
        int attempt = 0;

        do
        {
            if (shouldBePrime)
            {
                List<int> primes = GetPrimesInRange(minNumber, maxNumber);
                generatedValue = primes.Count > 0 ? primes[sharedRandom.Next(0, primes.Count)] : 2;
            }
            else
            {
                List<int> nonPrimes = new List<int>();
                for (int i = minNumber; i <= maxNumber; i++)
                {
                    if (!IsPrime(i))
                        nonPrimes.Add(i);
                }
                generatedValue =
                    nonPrimes.Count > 0 ? nonPrimes[sharedRandom.Next(0, nonPrimes.Count)] : 4;
            }
            attempt++;
        } while (recentValuesGeneral.Contains(generatedValue) && attempt < maxAttempts);

        recentValuesGeneral.Enqueue(generatedValue);
        if (recentValuesGeneral.Count > 8)
            recentValuesGeneral.Dequeue();

        return generatedValue;
    }

    private int GetCorrectBubbleChance()
    {
        if (levelIndex <= 10)
            return 75;
        else if (levelIndex <= 20)
            return 70;
        else if (levelIndex <= 30)
            return 65;
        else if (levelIndex <= 40)
            return 60;
        else if (levelIndex <= 50)
            return 58;
        else if (levelIndex <= 70)
            return 55;
        else
            return 52; // Minimum %52 doğru cevap şansı
    }

    private void OnBubbleClicked(GameObject bubble)
    {
        if (levelTransitioning || !bubbleDataMap.ContainsKey(bubble) || !bubble.GetComponent<Button>().interactable)
            return;

        BubbleData data = bubbleDataMap[bubble];

        Button btn = bubble.GetComponent<Button>();
        if (btn != null)
            btn.interactable = false;

        stem.DestroyAllSFX();

        bool isCorrect = CheckIfCorrect(data.value, bubble);

        if (isCorrect)
        {
            if (gameMode == BubbleGameMode.AdditionBubbles && currentSum < currentTargetNumber)
            {
                Image bubbleImg = bubble.GetComponent<Image>();
                if (bubbleImg != null)
                {
                    Color c = bubbleImg.color;
                    c.a = 0.5f;
                    bubbleImg.color = c;
                }

                PlaySfx(WinSFX);
                UpdateYonergeText();
                return;
            }

            PlayPopAnimation(bubble);
            PlaySfx(WinSFX);

            if (gameMode == BubbleGameMode.AdditionBubbles && currentSum == currentTargetNumber)
            {
                Debug.Log(
                    $"[CORRECT COMBO] {string.Join(" + ", selectedNumbers)} = {currentTargetNumber}"
                );

                foreach (var selectedBubble in selectedBubbles)
                {
                    if (selectedBubble != null && selectedBubble != bubble)
                    {
                        PlayPopAnimation(selectedBubble);
                    }
                }

                selectedNumbers.Clear();
                selectedBubbles.Clear();
                currentSum = 0;

                if (!levelTransitioning)
                {
                    StartCoroutine(HandleLevelComplete());
                }
            }
            else if (gameMode != BubbleGameMode.AdditionBubbles)
            {
                correctPops++;

                if (correctPops >= targetPopCount && !levelTransitioning)
                {
                    StartCoroutine(HandleLevelComplete());
                }
            }
        }
        else
        {
            if (gameMode == BubbleGameMode.AdditionBubbles)
            {
                Debug.Log(
                    $"[WRONG] Value: {data.value}, Sum would be: {currentSum + data.value}, Target: {currentTargetNumber}"
                );

                PlayPopAnimation(bubble);

                foreach (var selectedBubble in selectedBubbles)
                {
                    if (selectedBubble != null && selectedBubble != bubble)
                    {
                        PlayPopAnimation(selectedBubble);
                    }
                }

                selectedNumbers.Clear();
                selectedBubbles.Clear();
                currentSum = 0;
            }
            else
            {
                PlayPopAnimation(bubble);
            }

            PlaySfx(WrongSFX);
        }

        UpdateYonergeText();
    }

    private void PlayPopAnimation(GameObject bubble)
    {
        if (bubble == null)
            return;

        Image bubbleImg = bubble.GetComponent<Image>();
        if (bubbleImg != null && !bubbleImg.enabled)
        {
            Debug.LogWarning($"[PlayPopAnimation] {bubble.name} already popped!");
            return;
        }

        if (bubbleDataMap.ContainsKey(bubble))
        {
            if (bubbleImg != null)
            {
                bubbleImg.enabled = false;
                TextMeshProUGUI numberText = bubbleDataMap[bubble].text;
                if (numberText != null)
                    numberText.enabled = false;
            }
        }

        Transform explodingChild = bubble.transform.Find("Exploding");
        if (explodingChild != null)
        {
            explodingChild.gameObject.SetActive(true);

            float delay = 0.25f;

            StartCoroutine(ReturnBubbleAfterDelay(bubble, delay));
        }
        else
        {
            Debug.LogWarning($"[PlayPopAnimation] {bubble.name} has no Exploding child!");
            ReturnBubbleToPool(bubble);
        }
    }

    private bool CheckIfCorrect(int value, GameObject bubble)
    {
        BubbleGameMode activeMode = GetActiveMode();

        switch (activeMode)
        {
            case BubbleGameMode.AdditionBubbles:
                int potentialSum = currentSum + value;

                if (potentialSum == currentTargetNumber)
                {
                    selectedNumbers.Add(value);
                    selectedBubbles.Add(bubble);
                    currentSum = potentialSum;
                    return true;
                }
                else if (potentialSum > currentTargetNumber)
                {
                    return false;
                }
                else
                {
                    selectedNumbers.Add(value);
                    selectedBubbles.Add(bubble);
                    currentSum = potentialSum;
                    return true;
                }

            case BubbleGameMode.MultiplicationPop:
                return value % currentTargetNumber == 0;

            case BubbleGameMode.FactorBubbles:
                return currentTargetNumber % value == 0;

            case BubbleGameMode.PrimeBubbles:
                return IsPrime(value);

            case BubbleGameMode.GreaterThan:
                return value > currentTargetNumber;

            case BubbleGameMode.LessThan:
                return value < currentTargetNumber;

            default:
                return false;
        }
    }

    private IEnumerator ReturnBubbleAfterDelay(GameObject bubble, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (bubble != null)
        {
            Transform explodingChild = bubble.transform.Find("Exploding");
            if (explodingChild != null)
            {
                explodingChild.gameObject.SetActive(false);
            }

            ReturnBubbleToPool(bubble);
        }
    }

    private IEnumerator HandleLevelComplete()
    {
        if (levelTransitioning)
            yield break;

        levelTransitioning = true;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        ClearAllBubbles();

        TriggerFinale();

        // Random mode varsa, yeni modu seç
        SwitchRandomMode();

        yield return StartCoroutine(WaitForSfxComplete());

        levelIndex++;
        stem.FinalAnswer();
        ApplyDifficultyForLevel(levelIndex);
        correctPops = 0;

        selectedNumbers.Clear();
        selectedBubbles.Clear();
        currentSum = 0;

        StartLevel();

        levelTransitioning = false;
    }

    private IEnumerator WaitForSfxComplete()
    {
        if (SoundFX != null)
        {
            AudioSource audio = SoundFX.GetComponent<AudioSource>();
            if (audio != null && audio.isPlaying)
            {
                while (audio != null && audio.isPlaying)
                    yield return null;
            }
            else
            {
                yield return new WaitForSeconds(0.05f);
            }

            if (SoundFX != null)
            {
                Destroy(SoundFX);
                SoundFX = null;
            }
        }
    }

    private void TriggerFinale()
    {
        if (SoundFX != null)
        {
            Destroy(SoundFX);
            SoundFX = null;
        }

        if (WinSFXFinal != null)
        {
            SoundFX = Instantiate(WinSFXFinal);
            SoundFX.name = "WinSFXFinal";
        }
    }

    private void PlaySfx(GameObject prefab)
    {
        if (prefab == null)
            return;

        GameObject sfx = Instantiate(prefab);
        sfx.name = prefab.name;
        Destroy(sfx, 2f);
    }

    private int GenerateTargetNumber()
    {
        int min = Mathf.Max(2, minNumber);
        int max = Mathf.Max(min, maxNumber);

        BubbleGameMode activeMode = GetActiveMode();

        switch (activeMode)
        {
            case BubbleGameMode.AdditionBubbles:
                return sharedRandom.Next(Mathf.Max(10, min), max + 1);

            case BubbleGameMode.MultiplicationPop:
                int[] commonMults = { 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                return commonMults[
                    sharedRandom.Next(0, Mathf.Min(commonMults.Length, levelIndex / 5 + 3))
                ];

            case BubbleGameMode.FactorBubbles:
                int factorBase;
                int attempts = 0;

                // Sayı aralığı seviyeyle birlikte genişler.
                int levelScale = Mathf.Clamp(levelIndex, 1, 100);
                int low = Mathf.Max(6, minNumber);
                int high = Mathf.Min(maxNumber, 50 + levelScale * 5); // her seviye 5 artar (örn. lv50 → max 300)

                if (high < low)
                    high = low + 5;

                do
                {
                    factorBase = sharedRandom.Next(low, high + 1);
                    attempts++;
                } while (IsPrime(factorBase) && attempts < 20);

                List<int> factors = GetFactors(factorBase);
                int factorCount = factors.Count;

                if (factorCount < 4)
                    targetPopCount = factorCount;
                else
                    targetPopCount = Mathf.CeilToInt(factorCount / 2f);

                Debug.Log(
                    $"[FactorBubbles] Target={factorBase}, Range={low}-{high}, Factors={factorCount}, Level={levelIndex}"
                );
                return factorBase;
            case BubbleGameMode.PrimeBubbles:
                return 0;

            case BubbleGameMode.GreaterThan:
            case BubbleGameMode.LessThan:
                // Hedef sayı min ve max arasında bir değer (kendisi hariç)
                int range = max - min;
                if (range < 5)
                {
                    // Çok dar aralıksa, ortadan seç
                    return (min + max) / 2;
                }
                else
                {
                    // %20-%80 aralığında bir değer seç (uç değerlerden kaçın)
                    int offset = (int)(range * 0.2f);
                    return sharedRandom.Next(min + offset, max - offset + 1);
                }

            default:
                return 10;
        }
    }

    private List<int> GetFactors(int number)
    {
        List<int> factors = new List<int>();
        for (int i = 1; i <= number; i++)
        {
            if (number % i == 0)
                factors.Add(i);
        }
        return factors;
    }

    private bool IsPrime(int number)
    {
        if (number <= 1)
            return false;
        if (number == 2)
            return true;
        if (number % 2 == 0)
            return false;

        int sqrt = (int)Mathf.Sqrt(number);
        for (int i = 3; i <= sqrt; i += 2)
        {
            if (number % i == 0)
                return false;
        }
        return true;
    }

    private List<int> GetPrimesInRange(int min, int max)
    {
        List<int> primes = new List<int>();
        for (int i = min; i <= max; i++)
        {
            if (IsPrime(i))
                primes.Add(i);
        }
        return primes;
    }

    private void ApplyDifficultyForLevel(int level)
    {
        int clamped = Mathf.Min(level, 100);
        float cappedLevel = Mathf.Min(clamped, 50);

        int groupIndex = (int)(cappedLevel / 5);
        float wave = (Mathf.Sin(level * Mathf.PI / 9f) + 1f) * 0.5f;

        if (gameMode != BubbleGameMode.FactorBubbles)
        {
            float baseTarget = 3f + groupIndex * 0.7f;
            float waveTarget = Mathf.Lerp(0f, 1.0f, wave);
            targetPopCount = Mathf.RoundToInt(Mathf.Min(baseTarget + waveTarget, 10));
        }

        float baseSpawn = Mathf.Lerp(2.8f, 1.5f, cappedLevel / 50f);
        float waveSpawn = Mathf.Lerp(-0.1f, 0.1f, Mathf.Sin(level * Mathf.PI / 6f));
        spawnInterval = Mathf.Clamp(baseSpawn + waveSpawn, 1.0f, 3.0f);

        float baseSpeed = Mathf.Lerp(1.2f, 2.5f, cappedLevel / 50f);
        float waveSpeed = Mathf.Lerp(-0.15f, 0.15f, Mathf.Sin(level * Mathf.PI / 12f));
        bubbleSpeed = Mathf.Clamp(baseSpeed + waveSpeed, 1.0f, 3.0f); // Max 3.0 hız

        float baseMax = Mathf.Lerp(5f, 9f, cappedLevel / 50f);
        float waveMax = Mathf.Lerp(-0.3f, 0.3f, Mathf.Sin(level * Mathf.PI / 8f));
        maxBubbles = Mathf.RoundToInt(Mathf.Clamp(baseMax + waveMax, 5f, 10f)); // Max 10 baloncuk
        currentTargetNumber = GenerateTargetNumber();

        Vector2 range;

        if (gameMode == BubbleGameMode.FactorBubbles)
        {
            // Çarpan modunda sayı aralığı hedefe göre belirlenir.
            int offset = Mathf.Clamp(currentTargetNumber / 2, 10, 40);
            int low = Mathf.Max(2, currentTargetNumber - offset);
            int high = Mathf.Min(currentTargetNumber + offset, 200);
            range = new Vector2(low, high);

            // hedefin faktör sayısına göre zorluk belirle
            List<int> factors = GetFactors(currentTargetNumber);
            int factorCount = factors.Count;
            targetPopCount = Mathf.Clamp(Mathf.CeilToInt(factorCount / 1.5f), 2, 8);
        }
        else
        {
            range = GetNumberRangeForLevel(level);
            float expand = Mathf.Lerp(0f, 15f, Mathf.Sin(level * Mathf.PI / 15f) * 0.5f + 0.5f);
            range.y += expand;
        }

        int rx = Mathf.RoundToInt(range.x);
        int ry = Mathf.RoundToInt(range.y);

        // garanti: ry < rx olmasın
        if (ry < rx)
            ry = rx;

        // enforce minimums
        rx = Mathf.Max(1, rx);
        ry = Mathf.Max(rx, ry);

        // assign Vector2Int ve cache değerler
        numberRangeXY = new Vector2Int(rx, ry);
        minNumber = rx;
        maxNumber = ry;

        Debug.Log(
            $"[BUBBLE LEVEL {level}] Mode={gameMode}, Target={currentTargetNumber}, "
                + $"Range={minNumber}-{maxNumber}, Speed={bubbleSpeed:F2}, Interval={spawnInterval:F2}"
        );

        UpdateYonergeText();
    }

    private Vector2 GetNumberRangeForLevel(int level)
    {
        BubbleGameMode activeMode = GetActiveMode();

        float minValue = (activeMode == BubbleGameMode.PrimeBubbles) ? 2f : 1f;

        if (level >= 50)
        {
            switch (activeMode)
            {
                case BubbleGameMode.AdditionBubbles:
                    return new Vector2(minValue, 500);
                case BubbleGameMode.MultiplicationPop:
                    return new Vector2(minValue, 1000);
                case BubbleGameMode.FactorBubbles:
                    return new Vector2(minValue, 800);
                case BubbleGameMode.PrimeBubbles:
                    return new Vector2(2, 500);
                case BubbleGameMode.GreaterThan:
                case BubbleGameMode.LessThan:
                    return new Vector2(minValue, 500);
                default:
                    return new Vector2(minValue, 500);
            }
        }

        int groupIndex = level / 5;
        int levelInGroup = level % 5;

        float wavePhase = levelInGroup * Mathf.PI * 0.5f;
        float wave = Mathf.Sin(wavePhase) * 0.5f + 0.5f;

        switch (activeMode)
        {
            case BubbleGameMode.AdditionBubbles:
                minValue = CalculateMinForAddition(groupIndex);
                float maxAddition = CalculateMaxForAddition(groupIndex, wave);
                return new Vector2(minValue, maxAddition);

            case BubbleGameMode.MultiplicationPop:
                float maxMultiplication = CalculateMaxForMultiplication(groupIndex, wave);
                return new Vector2(minValue, maxMultiplication);

            case BubbleGameMode.FactorBubbles:
                minValue = CalculateMinForFactor(groupIndex);
                float maxFactor = CalculateMaxForFactor(groupIndex, wave);
                return new Vector2(minValue, maxFactor);

            case BubbleGameMode.PrimeBubbles:
                float maxPrime = CalculateMaxForPrime(groupIndex, wave);
                return new Vector2(2, maxPrime);

            case BubbleGameMode.GreaterThan:
            case BubbleGameMode.LessThan:
                minValue = CalculateMinForComparison(groupIndex);
                float maxComparison = CalculateMaxForComparison(groupIndex, wave);
                return new Vector2(minValue, maxComparison);

            default:
                return new Vector2(minValue, 50);
        }
    }

    private float CalculateMinForAddition(int groupIndex)
    {
        if (groupIndex == 0)
            return 4;
        if (groupIndex == 1)
            return 4;
        if (groupIndex <= 3)
            return 10;
        if (groupIndex <= 5)
            return 25;
        if (groupIndex <= 7)
            return 50;
        return 100;
    }

    private float CalculateMaxForAddition(int groupIndex, float wave)
    {
        float baseMax,
            rangeSpread;

        if (groupIndex == 0)
        {
            baseMax = 20;
            rangeSpread = 6;
        }
        else if (groupIndex == 1)
        {
            baseMax = 60;
            rangeSpread = 40;
        }
        else if (groupIndex == 2)
        {
            baseMax = 120;
            rangeSpread = 80;
        }
        else if (groupIndex == 3)
        {
            baseMax = 180;
            rangeSpread = 100;
        }
        else if (groupIndex == 4)
        {
            baseMax = 250;
            rangeSpread = 120;
        }
        else if (groupIndex == 5)
        {
            baseMax = 300;
            rangeSpread = 100;
        }
        else if (groupIndex == 6)
        {
            baseMax = 350;
            rangeSpread = 100;
        }
        else if (groupIndex == 7)
        {
            baseMax = 400;
            rangeSpread = 100;
        }
        else if (groupIndex == 8)
        {
            baseMax = 450;
            rangeSpread = 100;
        }
        else
        {
            baseMax = 500;
            rangeSpread = 100;
        }

        return baseMax - rangeSpread * (1f - wave);
    }

    private float CalculateMaxForMultiplication(int groupIndex, float wave)
    {
        float baseMax,
            rangeSpread;

        if (groupIndex == 0)
        {
            baseMax = 50;
            rangeSpread = 30;
        }
        else if (groupIndex == 1)
        {
            baseMax = 150;
            rangeSpread = 70;
        }
        else if (groupIndex == 2)
        {
            baseMax = 250;
            rangeSpread = 100;
        }
        else if (groupIndex == 3)
        {
            baseMax = 400;
            rangeSpread = 150;
        }
        else if (groupIndex == 4)
        {
            baseMax = 550;
            rangeSpread = 150;
        }
        else if (groupIndex == 5)
        {
            baseMax = 700;
            rangeSpread = 150;
        }
        else if (groupIndex == 6)
        {
            baseMax = 800;
            rangeSpread = 100;
        }
        else if (groupIndex == 7)
        {
            baseMax = 900;
            rangeSpread = 100;
        }
        else if (groupIndex == 8)
        {
            baseMax = 950;
            rangeSpread = 50;
        }
        else
        {
            baseMax = 1000;
            rangeSpread = 50;
        }

        return baseMax - rangeSpread * (1f - wave);
    }

    private float CalculateMinForFactor(int groupIndex)
    {
        if (groupIndex == 0)
            return 5;
        if (groupIndex == 1)
            return 10;
        if (groupIndex <= 3)
            return 20;
        if (groupIndex <= 5)
            return 40;
        if (groupIndex <= 7)
            return 80;
        return 10;
    }

    private float CalculateMaxForFactor(int groupIndex, float wave)
    {
        float baseMax,
            rangeSpread;

        if (groupIndex == 0)
        {
            baseMax = 30;
            rangeSpread = 15;
        }
        else if (groupIndex == 1)
        {
            baseMax = 80;
            rangeSpread = 40;
        }
        else if (groupIndex == 2)
        {
            baseMax = 150;
            rangeSpread = 70;
        }
        else if (groupIndex == 3)
        {
            baseMax = 250;
            rangeSpread = 100;
        }
        else if (groupIndex == 4)
        {
            baseMax = 350;
            rangeSpread = 100;
        }
        else if (groupIndex == 5)
        {
            baseMax = 450;
            rangeSpread = 100;
        }
        else if (groupIndex == 6)
        {
            baseMax = 550;
            rangeSpread = 100;
        }
        else if (groupIndex == 7)
        {
            baseMax = 650;
            rangeSpread = 100;
        }
        else if (groupIndex == 8)
        {
            baseMax = 750;
            rangeSpread = 100;
        }
        else
        {
            baseMax = 800;
            rangeSpread = 50;
        }

        return baseMax - rangeSpread * (1f - wave);
    }

    private float CalculateMaxForPrime(int groupIndex, float wave)
    {
        float baseMax,
            rangeSpread;

        if (groupIndex == 0)
        {
            baseMax = 30;
            rangeSpread = 15;
        }
        else if (groupIndex == 1)
        {
            baseMax = 70;
            rangeSpread = 30;
        }
        else if (groupIndex == 2)
        {
            baseMax = 120;
            rangeSpread = 50;
        }
        else if (groupIndex == 3)
        {
            baseMax = 180;
            rangeSpread = 60;
        }
        else if (groupIndex == 4)
        {
            baseMax = 250;
            rangeSpread = 70;
        }
        else if (groupIndex == 5)
        {
            baseMax = 320;
            rangeSpread = 70;
        }
        else if (groupIndex == 6)
        {
            baseMax = 380;
            rangeSpread = 60;
        }
        else if (groupIndex == 7)
        {
            baseMax = 430;
            rangeSpread = 50;
        }
        else if (groupIndex == 8)
        {
            baseMax = 470;
            rangeSpread = 40;
        }
        else
        {
            baseMax = 500;
            rangeSpread = 30;
        }

        return baseMax - rangeSpread * (1f - wave);
    }

    private float CalculateMinForComparison(int groupIndex)
    {
        if (groupIndex == 0)
            return 1;
        if (groupIndex == 1)
            return 5;
        if (groupIndex <= 3)
            return 10;
        if (groupIndex <= 5)
            return 20;
        if (groupIndex <= 7)
            return 40;
        return 80;
    }

    private float CalculateMaxForComparison(int groupIndex, float wave)
    {
        float baseMax,
            rangeSpread;

        if (groupIndex == 0)
        {
            baseMax = 20;
            rangeSpread = 10;
        }
        else if (groupIndex == 1)
        {
            baseMax = 50;
            rangeSpread = 20;
        }
        else if (groupIndex == 2)
        {
            baseMax = 100;
            rangeSpread = 40;
        }
        else if (groupIndex == 3)
        {
            baseMax = 150;
            rangeSpread = 50;
        }
        else if (groupIndex == 4)
        {
            baseMax = 200;
            rangeSpread = 60;
        }
        else if (groupIndex == 5)
        {
            baseMax = 280;
            rangeSpread = 80;
        }
        else if (groupIndex == 6)
        {
            baseMax = 350;
            rangeSpread = 70;
        }
        else if (groupIndex == 7)
        {
            baseMax = 420;
            rangeSpread = 70;
        }
        else if (groupIndex == 8)
        {
            baseMax = 470;
            rangeSpread = 50;
        }
        else
        {
            baseMax = 500;
            rangeSpread = 30;
        }

        return baseMax - rangeSpread * (1f - wave);
    }

    private void UpdateYonergeText()
    {
        if (yonergeText == null)
            return;

        stringBuilder.Clear();

        BubbleGameMode activeMode = GetActiveMode();

        switch (activeMode)
        {
            case BubbleGameMode.AdditionBubbles:
                stringBuilder.Append("Toplamı ");
                stringBuilder.Append(currentTargetNumber);
                stringBuilder.Append(" yapan baloncukları patlat!");

                if (selectedNumbers.Count > 0)
                {
                    stringBuilder.Append("\nŞu an: ");
                    for (int i = 0; i < selectedNumbers.Count; i++)
                    {
                        if (i > 0)
                            stringBuilder.Append(" + ");
                        stringBuilder.Append(selectedNumbers[i]);
                    }
                    stringBuilder.Append(" = ");
                    stringBuilder.Append(currentSum);
                }
                break;

            case BubbleGameMode.MultiplicationPop:
                stringBuilder.Append(currentTargetNumber);
                stringBuilder.Append("'nin katlarını patlat!");
                stringBuilder.Append("\n(");
                stringBuilder.Append(correctPops);
                stringBuilder.Append("/");
                stringBuilder.Append(targetPopCount);
                stringBuilder.Append(")");
                break;

            case BubbleGameMode.FactorBubbles:
                stringBuilder.Append(currentTargetNumber);
                stringBuilder.Append("'nin çarpanlarını patlat!");
                stringBuilder.Append("\n(");
                stringBuilder.Append(correctPops);
                stringBuilder.Append("/");
                stringBuilder.Append(targetPopCount);
                stringBuilder.Append(")");
                break;

            case BubbleGameMode.PrimeBubbles:
                stringBuilder.Append("Sadece asal sayıları patlat!");
                stringBuilder.Append("\n(");
                stringBuilder.Append(correctPops);
                stringBuilder.Append("/");
                stringBuilder.Append(targetPopCount);
                stringBuilder.Append(")");
                break;

            case BubbleGameMode.GreaterThan:
                stringBuilder.Append(currentTargetNumber);
                stringBuilder.Append("'den büyük sayıları patlat!");
                stringBuilder.Append("\n(");
                stringBuilder.Append(correctPops);
                stringBuilder.Append("/");
                stringBuilder.Append(targetPopCount);
                stringBuilder.Append(")");
                break;

            case BubbleGameMode.LessThan:
                stringBuilder.Append(currentTargetNumber);
                stringBuilder.Append("'den küçük sayıları patlat!");
                stringBuilder.Append("\n(");
                stringBuilder.Append(correctPops);
                stringBuilder.Append("/");
                stringBuilder.Append(targetPopCount);
                stringBuilder.Append(")");
                break;
        }

        yonergeText.SetText(stringBuilder);
    }

    private struct BubbleData
    {
        public int value;
        public RectTransform rect;
        public TextMeshProUGUI text;
    }
}

[System.Serializable]
public enum BubbleGameMode
{
    AdditionBubbles,
    MultiplicationPop,
    FactorBubbles,
    PrimeBubbles,
    GreaterThan,
    LessThan,
}

