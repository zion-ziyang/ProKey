using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PhraseProvider : MonoBehaviour
{
    public static PhraseProvider Instance { get; private set; }

    [SerializeField] private TextAsset phraseFile;

    [Tooltip("Study 3 phrase sets, one file per day (element 0 = Day 1). Selected with SetDay().")]
    [SerializeField] private TextAsset[] dayPhraseFiles;

    private List<string> allPhrases = new List<string>(); // Currently active phrase set (global set or a day set)
    private List<string> globalPhrases = new List<string>();
    private bool phrasesLoaded = false;

    // --- Persistence Logic ---
    private int currentPhraseIndex = 0;
    private const string PhraseIndexKey = "CurrentStudyPhraseIndex"; // Key for PlayerPrefs

    // --- Day Sets (Study 3) ---
    private int activeDay = 0; // 0 = global phrase set, 1..N = day set
    private int globalPhraseIndex = 0;
    // Progress inside each day set, kept for the lifetime of the app so that re-entering
    // the same day (e.g. after an accidental "End") does not repeat phrases
    private readonly Dictionary<int, int> dayPhraseIndices = new Dictionary<int, int>();


    private void Awake()
    {
        if (Instance != null && Instance != this) 
        {
            Destroy(this.gameObject);
        }
        else 
        {
            Instance = this;
            // On awake, load last saved index from PlayerPrefs
            // Defaults to 0 on first run if key does not exist
            currentPhraseIndex = PlayerPrefs.GetInt(PhraseIndexKey, 0);
            globalPhraseIndex = currentPhraseIndex;
        }
    }

    void Start()
    {
        LoadPhrases();
    }

    // Automatically save current progress on application quit
    private void OnApplicationQuit()
    {
        if (activeDay == 0) globalPhraseIndex = currentPhraseIndex;
        PlayerPrefs.SetInt(PhraseIndexKey, globalPhraseIndex);
        PlayerPrefs.Save(); // Ensure data written to disk
        Debug.Log($"Application quit, saved phrase progress, current index: {currentPhraseIndex}");
    }

    private static List<string> ParsePhrases(TextAsset file)
    {
        return file.text.Split('\n')
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim()) // Remove potential leading/trailing whitespace and newlines
            .ToList();
    }

    private void LoadPhrases()
    {
        if (phrasesLoaded) return;
        phrasesLoaded = true;

        if (phraseFile != null)
        {
            globalPhrases = ParsePhrases(phraseFile);
            allPhrases = globalPhrases;
            
            if (allPhrases.Count > 0)
            {
                 Debug.Log($"Phrases loaded successfully: {allPhrases.Count} total. Current starting index: {currentPhraseIndex}");
            }
            else
            {
                Debug.LogError("Phrase file is empty or formatted incorrectly!");
            }
        }
        else
        {
            Debug.LogError("Phrase file not specified in PhraseProvider!");
        }
    }

    /// <summary>
    /// Retrieves the next sequential, cyclic set of phrases.
    /// </summary>
    /// <param name="count">Number of phrases to retrieve</param>
    /// <returns>A queue containing the specified number of phrases</returns>
    public Queue<string> GetNextPhraseQueue(int count)
    {
        LoadPhrases();
        var phraseQueue = TakePhrases(allPhrases, ref currentPhraseIndex, count);

        if (activeDay == 0)
        {
            // Important: save after each retrieval to prevent progress loss in case of crash
            globalPhraseIndex = currentPhraseIndex;
            PlayerPrefs.SetInt(PhraseIndexKey, currentPhraseIndex);
        }
        else
        {
            dayPhraseIndices[activeDay] = currentPhraseIndex;
        }

        return phraseQueue;
    }

    /// <summary>
    /// Practice phrases always come from the global set with its persistent index. While a Study 3 day set is
    /// active this leaves the day set untouched, so all of it is available to the test blocks (2 methods x 2 blocks
    /// x 5 phrases = 20 of the 26 phrases, none repeated within a day).
    /// </summary>
    public Queue<string> GetPracticePhraseQueue(int count)
    {
        LoadPhrases();
        if (activeDay == 0) return GetNextPhraseQueue(count);

        var phraseQueue = TakePhrases(globalPhrases, ref globalPhraseIndex, count);
        PlayerPrefs.SetInt(PhraseIndexKey, globalPhraseIndex);
        return phraseQueue;
    }

    /// <summary>Takes the next <paramref name="count"/> phrases of a list in order, wrapping around at its end (cyclic)</summary>
    private static Queue<string> TakePhrases(List<string> phrases, ref int index, int count)
    {
        var phraseQueue = new Queue<string>();
        if (phrases.Count == 0)
        {
            Debug.LogError("Cannot retrieve phrases because the phrase list is empty!");
            return phraseQueue;
        }

        for (int i = 0; i < count; i++)
        {
            if (index >= phrases.Count) index = 0;
            phraseQueue.Enqueue(phrases[index]);
            index++; // Advance to next index in preparation for next call
        }

        return phraseQueue;
    }

    /// <summary>
    /// Study 3: switches to the phrase set of the given day (1-based). Each day set starts at its
    /// first phrase and is consumed sequentially by the test blocks of both methods, so no phrase repeats
    /// within a day as long as the day's test trial count does not exceed the size of the set.
    /// </summary>
    /// <returns>true if the day set was found and activated</returns>
    public bool SetDay(int day)
    {
        LoadPhrases();

        if (dayPhraseFiles == null || day < 1 || day > dayPhraseFiles.Length || dayPhraseFiles[day - 1] == null)
        {
            Debug.LogError($"PhraseProvider: no phrase file assigned for Day {day}. Falling back to the global phrase set.", this);
            UseGlobalPhraseSet();
            return false;
        }

        List<string> dayPhrases = ParsePhrases(dayPhraseFiles[day - 1]);
        if (dayPhrases.Count == 0)
        {
            Debug.LogError($"PhraseProvider: phrase file for Day {day} is empty. Falling back to the global phrase set.", this);
            UseGlobalPhraseSet();
            return false;
        }

        if (activeDay == 0) globalPhraseIndex = currentPhraseIndex;

        activeDay = day;
        allPhrases = dayPhrases;
        if (!dayPhraseIndices.TryGetValue(day, out currentPhraseIndex))
        {
            currentPhraseIndex = 0;
        }

        Debug.Log($"PhraseProvider: Day {day} phrase set active ({allPhrases.Count} phrases, starting index {currentPhraseIndex}).");
        DataLogger.Instance.Log("PHRASE_SET", $"Day{day}", $"Count:{allPhrases.Count}|StartIndex:{currentPhraseIndex}");
        return true;
    }

    /// <summary>
    /// Studies 1 and 2: switches back to the global phrase set with its persistent index.
    /// </summary>
    public void UseGlobalPhraseSet()
    {
        LoadPhrases();
        if (activeDay == 0) return;

        activeDay = 0;
        allPhrases = globalPhrases;
        currentPhraseIndex = globalPhraseIndex;
        DataLogger.Instance.Log("PHRASE_SET", "Global", $"Count:{allPhrases.Count}|StartIndex:{currentPhraseIndex}");
    }

}