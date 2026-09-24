using UnityEngine;
using System;
using System.IO;

public class DataLogger : MonoBehaviour
{
    public static DataLogger Instance { get; private set; }

    private string currentFilePath;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject); // Keep logger alive across scene transitions
        }
    }

    /// <summary>
    /// Initializes a new log file for a new test phase.
    /// </summary>
    /// <param name="participantId">Participant ID</param>
    /// <param name="conditionInfo">Test condition description</param>
    public void InitializeNewLogFile(string participantId, string conditionInfo)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"P{participantId}_{conditionInfo.Replace(" ", "_")}_{timestamp}.csv";
        currentFilePath = Path.Combine(Application.persistentDataPath, fileName);

        // Write file header information
        try
        {
            string header = "Timestamp,EventType,Value,Details\n";
            File.WriteAllText(currentFilePath, header);
            Debug.Log($"Data log file created: {currentFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create log file: {e.Message}");
        }
    }

    /// <summary>
    /// Logs an event.
    /// </summary>
    /// <param name="eventType">Event type (e.g., KEY_PRESS, PHASE_START)</param>
    /// <param name="value">Value related to event (e.g., 'q', 'Practice')</param>
    /// <param name="details">Additional information (e.g., Target phrase)</param>
    public void Log(string eventType, string value, string details = "")
    {
        if (string.IsNullOrEmpty(currentFilePath))
        {
            Debug.LogWarning("Data logger not initialized; please call InitializeNewLogFile first.");
            return;
        }

        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line = $"{timestamp},{eventType},{value},{details}\n";
            File.AppendAllText(currentFilePath, line);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to write log: {e.Message}");
        }
    }
}