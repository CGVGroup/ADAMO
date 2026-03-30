using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using ActionSystem;
using HumanoidInteraction;
using UnityEngine;

/// <summary>
/// A utility class to log AgentActions to a structured string.
/// It uses reflection to dynamically serialize action data, so it can handle any new AgentAction type without modification.
/// </summary>
public static class ActionLogger
{
    /// <summary>
    /// Generates a structured string log for a list of AgentActions.
    /// </summary>
    /// <param name="actions">The list of actions to log.</param>
    /// <returns>A formatted string representing the list of actions and their properties.</returns>
    public static string LogActions(List<AgentAction> actions)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("Action Log:");
        stringBuilder.AppendLine("-----------");

        foreach (var action in actions)
        {
            LogAction(action, stringBuilder, 1);
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Recursively logs a single AgentAction and its properties.
    /// If the action is a composite, it will also log its sub-actions.
    /// </summary>
    /// <param name="action">The action to log.</param>
    /// <param name="stringBuilder">The StringBuilder to append the log to.</param>
    /// <param name="indentationLevel">The current indentation level for formatting.</param>
    private static void LogAction(AgentAction action, StringBuilder stringBuilder, int indentationLevel)
    {
        if (action == null)
        {
            Debug.LogError("Action is null!");
            stringBuilder.Append(GetIndentation(indentationLevel)).AppendLine("[!!! NULL Action !!!]");
            return;
        }

        stringBuilder.Append(GetIndentation(indentationLevel)).AppendLine($"[{action.GetType().Name}]");

        // Use reflection to get all fields (public, non-public, instance)
        FieldInfo[] fields = action.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var field in fields)
        {
            // Skip logging fields from the base AgentAction class unless necessary
            if (field.DeclaringType == typeof(AgentAction) && 
                field.Name != "state" && field.Name != "m_failCode" && field.Name != "log" &&
                field.Name != "m_startDateTime" && field.Name != "m_endDateTime")
            {
                 // You can add base class fields here if you want them in the log for every action.
                 // For now, we get them via public properties below.
            }
            
            // Skipping logging for AgentAction related System.Action
            if (field.FieldType == typeof(Action<Interaction>))
                continue;
            // Skipping logging for base64 images
            if (field.FieldType == typeof(string) && field.Name.ToLower().Contains("base64"))
            {
                stringBuilder.Append(GetIndentation(indentationLevel + 1)).AppendLine($"{field.Name}: [Base64 IMAGE]");
                continue;
            }
            
            var value = field.GetValue(action);
            stringBuilder.Append(GetIndentation(indentationLevel + 1)).AppendLine($"{field.Name}: {FormatValue(value, indentationLevel + 1)}");
        }
        
        // Log public properties from the base class for a cleaner output
        stringBuilder.Append(GetIndentation(indentationLevel + 1)).AppendLine($"StartTime: {action.StartTime}");
        stringBuilder.Append(GetIndentation(indentationLevel + 1)).AppendLine($"EndTime: {action.EndTime}");
        stringBuilder.Append(GetIndentation(indentationLevel + 1)).AppendLine($"State: {action.State}");
        if (action.FailCode != null)
        {
            stringBuilder.Append(GetIndentation(indentationLevel + 1)).AppendLine($"FailCode: {action.FailCode}");
        }
        stringBuilder.Append(GetIndentation(indentationLevel + 1)).AppendLine($"Log: {action.Log}");


                // If it's a composite action, log all its sub-actions recursively
        if (action is AgentCompositeAction compositeAction)
        {
            // Log Current Sub-Action
            FieldInfo currentActionField = typeof(AgentCompositeAction).GetField("currentSubAction", BindingFlags.NonPublic | BindingFlags.Instance);
            if (currentActionField != null)
            {
                var currentAction = currentActionField.GetValue(compositeAction) as AgentAction;
                if (currentAction != null)
                {
                    stringBuilder.Append(GetIndentation(indentationLevel + 1)).AppendLine("Current Sub-Action:");
                    LogAction(currentAction, stringBuilder, indentationLevel + 2);
                }
            }

            // Log Past Sub-Actions
            FieldInfo pastActionsField = typeof(AgentCompositeAction).GetField("pastSubActions", BindingFlags.NonPublic | BindingFlags.Instance);
            if (pastActionsField != null)
            {
                var pastActions = pastActionsField.GetValue(compositeAction) as List<AgentAction>;
                if (pastActions != null && pastActions.Count > 0)
                {
                    stringBuilder.Append(GetIndentation(indentationLevel + 1)).AppendLine("Past Sub-Actions:");
                    foreach (var subAction in pastActions)
                    {
                        LogAction(subAction, stringBuilder, indentationLevel + 2);
                    }
                }
            }

            // Log Queued Sub-Actions
            FieldInfo queuedActionsField = typeof(AgentCompositeAction).GetField("subActionsQueue", BindingFlags.NonPublic | BindingFlags.Instance);
            if (queuedActionsField != null)
            {
                var queuedActions = queuedActionsField.GetValue(compositeAction) as List<AgentAction>;
                if (queuedActions != null && queuedActions.Count > 0)
                {
                    stringBuilder.Append(GetIndentation(indentationLevel + 1)).AppendLine("Queued Sub-Actions:");
                    foreach (var subAction in queuedActions)
                    {
                        LogAction(subAction, stringBuilder, indentationLevel + 2);
                    }
                }
            }
        }
        stringBuilder.AppendLine();
    }
    
    /// <summary>
    /// Formats a value for logging. Handles common Unity types.
    /// </summary>
    private static string FormatValue(object value, int indentation)
    {
        if (value == null)
        {
            return "null";
        }
        if (value is Vector3 vec3)
        {
            return vec3.ToString("F3"); // Format to 3 decimal places
        }
        if (value is Transform transform)
        {
            return $"Transform(Name: '{transform.name}', Position: {transform.position:F3})";
        }
        if (value is GameObject go)
        {
            return $"GameObject(Name: '{go.name}')";
        }
        // Use only for ADAMO Agent Tool Calling DEBUG!
        if (value is Pickable pickable)
        {
            return $"ObjectTag(StringID: '{pickable.GetComponent<ObjectTag>().GetStringId()}')";
        }

        if (value is UnityGameObjectData)
        {
            UnityGameObjectData objData = (UnityGameObjectData) value;
            //EXAMPLE from LangChain: - stringId=Screwdriver_5 position=(x=-0.36, y=0.90, z=-1.10)
            return GetIndentation(indentation) + $"{objData.type}_{objData.id} ({objData.position.x}, {objData.position.y}, {objData.position.z})";
        }
        if (value is List<UnityGameObjectData>)
        {
            string objList = "";
            
            foreach (UnityGameObjectData obj in (List<UnityGameObjectData>) value)
            {
                objList += "\n";
                objList += FormatValue(obj,indentation + 1);
            }
            return objList;
        }
        // Add more custom formatting for other Unity or custom types as needed.
        
        return value.ToString();
    }


    /// <summary>
    /// Gets an indentation string based on the level.
    /// </summary>
    private static string GetIndentation(int level)
    {
        return new string(' ', level * 4); // 4 spaces per indentation level
    }
    
    /// <summary>
    /// Saves the provided log content to a text file at the specified path.
    /// It will create the file if it doesn't exist, or overwrite it if it does.
    /// </summary>
    /// <param name="logContent">The string content to save.</param>
    /// <param name="filePath">The full path of the file to save to (e.g., Application.persistentDataPath + "/action_log.txt").</param>
    private static bool SaveLogToFile(string logContent, string filePath)
    {
        try
        {
            // Ensure the directory exists before writing the file
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Debug.Log($"The directory {directory} doesn't exist, I will create it.");
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, logContent);
            Debug.Log($"Log successfully saved to: {filePath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save log file at {filePath}. Error: {e.Message}");
            return false;
        }
    }

    public static bool LogActionsToFile(List<AgentAction> actions, string folderName, string fileName)
    {
        string logContent = LogActions(actions);
        string filePath = Path.Combine(BenchmarkManager.BenchmarkFolderPath, folderName, fileName);
        return SaveLogToFile(logContent, filePath);
    }
}
