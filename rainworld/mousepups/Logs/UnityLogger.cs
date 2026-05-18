using System;
using System.Collections.Generic;
using UnityEngine;

namespace MicePups.Logs
{
    internal class UnityLogger : MonoBehaviour
    {
        private static UnityLogger instance;
        private static readonly Queue<string> logQueue = new Queue<string>();
        private static readonly int maxLines = 15;
        private static string currentLogText = "";

        public static void Initialize()
        {
            // Only initialize once
            if (instance == null)
            {
                // Create an invisible GameObject in the background to hold this script
                GameObject loggerObject = new GameObject("MicePups_OnGUILogger");
                instance = loggerObject.AddComponent<UnityLogger>();

                // Keep it alive even when transitioning between menus and the game
                DontDestroyOnLoad(loggerObject);

                // Subscribe to Unity's log events
                Application.logMessageReceived += HandleUnityLog;
            }
        }

        private static void HandleUnityLog(string logString, string stackTrace, LogType type)
        {
            string formattedMessage = $"[{type}] {logString}";

            logQueue.Enqueue(formattedMessage);
            if (logQueue.Count > maxLines)
            {
                logQueue.Dequeue();
            }

            // Update the combined string
            currentLogText = string.Join("\n", logQueue.ToArray());
        }

        // This runs every frame
        private void OnGUI()
        {
            if (string.IsNullOrEmpty(currentLogText)) return;

            // Set up a custom style
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 12;
            style.wordWrap = false;

            // Draw a black "drop shadow" slightly offset
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(22, 22, 1000, 800), currentLogText, style);

            // Draw the white text on top
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(20, 20, 1000, 800), currentLogText, style);
        }

        private void OnDestroy()
        {
            // Clean up the memory leak if the script is ever destroyed
            Application.logMessageReceived -= HandleUnityLog;
        }
    }
}
