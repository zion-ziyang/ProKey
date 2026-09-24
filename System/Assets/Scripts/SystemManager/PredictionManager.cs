using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class PredictionManager : MonoBehaviour, FKCoreListener
{
    [Header("SDK & UI References")]
    [SerializeField] private FKCoreXR fKCoreXR;
    [SerializeField] private GameObject predictionContainer;
    private List<BaseKey> predictionKeyBehaviours = new List<BaseKey>(); // Using base class type

    // --- Core data structure: using StringBuilder to internally manage complete text context ---
    private StringBuilder composedText = new StringBuilder();

    private bool isProcessingPrediction = false;

    /// <summary>
    /// Name of the licence file below a Resources folder (Assets/Resources/FleksyLicense.json).
    /// The file is not part of the repository: every user needs their own Fleksy key, see README.
    /// </summary>
    private const string FleksyLicenseResource = "FleksyLicense";

    [Serializable]
    private class FleksyLicense
    {
        public string licenseKey;
        public string licenseSecret;
    }

    void Start()
    {
        InitializeFleksySDK();
        KeyboardEvents.OnKeyPressed += HandleKeyPress;
        KeyboardEvents.OnDeleteCommandPressed += HandleDeleteCommand;
        KeyboardEvents.OnNextCommandPressed += HandleNextCommand;
        predictionContainer.SetActive(false);
    }

    private void OnDestroy()
    {
        KeyboardEvents.OnKeyPressed -= HandleKeyPress;
        KeyboardEvents.OnDeleteCommandPressed -= HandleDeleteCommand;
        KeyboardEvents.OnNextCommandPressed -= HandleNextCommand;
    }

    private static FleksyLicense LoadFleksyLicense()
    {
        TextAsset file = Resources.Load<TextAsset>(FleksyLicenseResource);
        if (file == null) return null;

        FleksyLicense license = JsonUtility.FromJson<FleksyLicense>(file.text);
        bool complete = license != null && !string.IsNullOrWhiteSpace(license.licenseKey) && !string.IsNullOrWhiteSpace(license.licenseSecret);
        return complete ? license : null;
    }

    private void InitializeFleksySDK()
    {
        fKCoreXR = transform.Find("/_FKCoreXR").GetComponentInChildren<FKCoreXR>();
        if (fKCoreXR == null) { Debug.LogError("FKCoreXR not assigned!"); return; }
        FleksyLicense license = LoadFleksyLicense();
        if (license == null)
        {
            Debug.LogError($"PredictionManager: no Fleksy licence found. Create Assets/Resources/{FleksyLicenseResource}.json with your own key and secret (see README). Typing works, word suggestions are disabled.");
            return;
        }

        fKCoreXR.StartFleksySDK(license.licenseKey, license.licenseSecret);
        fKCoreXR.setListener(this);
        Debug.Log("PredictionManager: Fleksy SDK Initialized.");
    }

    /// <summary>
    /// Registers a keyboard's prediction UI components and key behaviours. Called by external controllers (e.g. StudyFlowManager).
    /// </summary>
    /// <param name="container">Parent GameObject containing prediction keys</param>
    /// <param name="keys">List of BaseKey scripts for prediction keys</param>
    public void RegisterPredictionUI(GameObject container, List<BaseKey> keys)
    {
        Debug.Log("PredictionManager: Registering new prediction UI." + this);

        // If previously registered, hide the old one first
        if (predictionContainer != null)
        {
            predictionContainer.SetActive(false);
        }

        this.predictionContainer = container;
        this.predictionKeyBehaviours.Clear();
        this.predictionKeyBehaviours.AddRange(keys);

        composedText.Clear();

        // Ensure newly registered UI is initially hidden
        if (this.predictionContainer != null)
        {
            this.predictionContainer.SetActive(false);
        }
    }

    private void HandleKeyPress(string key)
    {
        if (isProcessingPrediction) return;

        // User selected a predicted word
        if (key.Length > 1)
        {
            // --- Core logic: update internal text state to match UI behavior ---
            // 1. Find the starting position of the current incomplete word being typed
            string currentText = composedText.ToString();
            int lastSpaceIndex = currentText.LastIndexOf(' ');
            int wordStartIndex = (lastSpaceIndex == -1) ? 0 : lastSpaceIndex + 1;

            // 2. Remove incomplete word
            composedText.Remove(wordStartIndex, composedText.Length - wordStartIndex);

            // 3. Append full predicted word and a space
            composedText.Append(key).Append(' ');

            StartCoroutine(ProcessPredictionSelectionRoutine());
        }
        // User pressed space
        else if (key == " ")
        {
            composedText.Append(' ');
            UpdateContextAndRequestPrediction();
        }
        // User pressed regular character
        else
        {
            composedText.Append(key);
            UpdateContextAndRequestPrediction();
        }
    }

    private void HandleDeleteCommand()
    {
        if (isProcessingPrediction) return;

        if (composedText.Length > 0)
        {
            // Delete one character from the end of the internal text state
            composedText.Length--;
            UpdateContextAndRequestPrediction();
        }
        else
        {
            HidePredictions();
        }
    }

    private void HandleNextCommand()
    {
        if (isProcessingPrediction) return;
        composedText.Clear();
        HidePredictions();
    }

    /// <summary>
    /// Analyzes current internal text context and requests appropriate predictions from the SDK
    /// </summary>
    private void UpdateContextAndRequestPrediction()
    {
        string currentText = composedText.ToString();

        if (string.IsNullOrEmpty(currentText))
        {
            HidePredictions();
            return;
        }

        // If text ends with a space, a word was just completed; request "next word prediction"
        if (currentText.EndsWith(" "))
        {
            fKCoreXR.NextWordPrediction(currentText, 1, currentText.Length);
        }
        // Otherwise, a word is currently being typed; request "current word prediction"
        else
        {
            int lastSpaceIndex = currentText.LastIndexOf(' ');
            string currentWord = (lastSpaceIndex == -1)
                ? currentText
                : currentText.Substring(lastSpaceIndex + 1);

            fKCoreXR.CurrentWordPrediction(currentWord, 1, currentWord.Length);
        }
    }

    // Waits until the suggestion keys finished their press animation, then shows the next suggestions
    private IEnumerator ProcessPredictionSelectionRoutine()
    {
        isProcessingPrediction = true;

        yield return new WaitUntil(() => predictionKeyBehaviours.All(key => !key.gameObject.activeInHierarchy || key.IsIdle()));

        foreach (var key in predictionKeyBehaviours)
        {
            if (key.gameObject.activeInHierarchy) { key.ResetKey(); }
        }

        HidePredictions(false);
        UpdateContextAndRequestPrediction();
        isProcessingPrediction = false;
    }

    #region --- SDK Callbacks & UI Update ---
    public void OnCurrentWordPredictions(ArrayList predictions)
    {
        if (isProcessingPrediction) return;
        UpdatePredictionUI(predictions);
        if (predictions.Count != 0)
        {

            string predictions_str = "";
            foreach (FKPredictionItem item in predictions)
            {

                if (predictions_str != "")
                {
                    predictions_str += "/";
                }

                predictions_str += item.label;

            }
            DataLogger.Instance.Log("PREDICTION_CURRENT", predictions_str);
        }
    }

    public void OnNextWordPredictions(ArrayList predictions)
    {
        UpdatePredictionUI(predictions);
        if (predictions.Count != 0)
        {

            string predictions_str = "";
            foreach (FKPredictionItem item in predictions)
            {

                if (predictions_str != "")
                {
                    predictions_str += "/";
                }

                predictions_str += item.label;

            }
            DataLogger.Instance.Log("PREDICTION_NEXT", predictions_str);
        }
    }
    public void OnSwipeWordPredictions(ArrayList arrayList) { /* Not handled currently */ }

    private void UpdatePredictionUI(ArrayList predictions)
    {
        // Add null checks for container and list
        if (predictionContainer == null || predictionKeyBehaviours.Count == 0) return;

        if (predictions == null || predictions.Count == 0)
        {
            HidePredictions();
            return;
        }

        predictionContainer.SetActive(true);
        for (int i = 0; i < predictionKeyBehaviours.Count; i++)
        {
            var keyBehaviour = predictionKeyBehaviours[i];
            if (i < predictions.Count)
            {
                FKPredictionItem item = (FKPredictionItem)predictions[i];
                keyBehaviour.keyValue = item.label.ToLower();

                // For robustness, find Text component downward from key root object
                TMP_Text keyTitle = keyBehaviour.GetComponentInChildren<TMP_Text>();
                if (keyTitle != null) { keyTitle.text = item.label; }

                keyBehaviour.gameObject.SetActive(true);
            }
            else
            {
                keyBehaviour.gameObject.SetActive(false);
            }
        }
    }
    /// <summary>
    /// hardHide: hides the whole suggestion row. Otherwise the keys stay visible and only lose their words
    /// (used right after a suggestion was chosen, until the next suggestions arrive).
    /// </summary>
    private void HidePredictions(bool hardHide = true)
    {
        if (hardHide)
        {
            if (predictionContainer != null) predictionContainer.SetActive(false);
            return;
        }

        foreach (var keyBehaviour in predictionKeyBehaviours)
        {
            if (keyBehaviour == null || !keyBehaviour.gameObject.activeSelf) continue;

            keyBehaviour.keyValue = string.Empty;
            TMP_Text keyTitle = keyBehaviour.GetComponentInChildren<TMP_Text>();
            if (keyTitle != null) keyTitle.text = string.Empty;
        }
    }
    #endregion

    public void ResetPredictions()
    {
        composedText.Clear();
        HidePredictions();
    }
}