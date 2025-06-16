using System;
using System.Linq;
using System.Reflection;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for automating Builder Browser interactions and Odin Inspector analysis
    /// Enables programmatic testing of the Curation Engine transpiler development
    /// </summary>
    public class BuilderBrowserAutomationTool : McpToolBase
    {
        public BuilderBrowserAutomationTool()
        {
            Name = "builder_browser_automation";
            Description = "Automate Builder Browser interactions for Curation Engine Odin→App UI transpiler testing";
            IsAsync = true; // UI operations need main thread
        }
        
        /// <summary>
        /// Execute Builder Browser automation asynchronously
        /// </summary>
        /// <param name="parameters">Tool parameters</param>
        /// <param name="tcs">Task completion source</param>
        public override void ExecuteAsync(JObject parameters, System.Threading.Tasks.TaskCompletionSource<JObject> tcs)
        {
            // Schedule on main thread since we're dealing with Unity Editor UI
            EditorApplication.delayCall += () =>
            {
                try
                {
                    var result = ExecuteBuilderBrowserAutomation(parameters);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    McpLogger.LogError($"[Builder Browser Automation] Error: {ex.Message}");
                    tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(ex.Message, "automation_error"));
                }
            };
        }
        
        private JObject ExecuteBuilderBrowserAutomation(JObject parameters)
        {
            string action = parameters["action"]?.ToObject<string>() ?? "analyze";
            string builderName = parameters["builderName"]?.ToObject<string>();
            
            switch (action.ToLower())
            {
                case "open_window":
                    return OpenBuilderBrowser();
                    
                case "analyze_builder":
                    if (string.IsNullOrEmpty(builderName))
                        return McpUnitySocketHandler.CreateErrorResponse("builderName parameter required for analyze_builder action", "validation_error");
                    return AnalyzeBuilder(builderName);
                    
                case "list_builders":
                    return ListAllBuilders();
                    
                case "test_tabgroup_detection":
                    return TestTabGroupDetection();
                    
                default:
                    return McpUnitySocketHandler.CreateErrorResponse($"Unknown action: {action}. Available: open_window, analyze_builder, list_builders, test_tabgroup_detection", "validation_error");
            }
        }
        
        private JObject OpenBuilderBrowser()
        {
            try
            {
                // Open the Builder Browser window
                EditorApplication.ExecuteMenuItem("Window/Curation Engine/Builder Browser");
                
                McpLogger.LogInfo("[Builder Browser Automation] Builder Browser window opened");
                
                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = "✅ Builder Browser window opened successfully"
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"Failed to open Builder Browser: {ex.Message}", "ui_error");
            }
        }
        
        private JObject AnalyzeBuilder(string builderName)
        {
            try
            {
                // Find the builder type by name (simplified search)
                var builderTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(assembly => assembly.GetTypes())
                    .Where(type => type.Name.Contains("Builder") && 
                                   type.Name.IndexOf(builderName.Replace(" ", "_"), StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                
                if (!builderTypes.Any())
                {
                    return McpUnitySocketHandler.CreateErrorResponse($"No builder found matching: {builderName}", "not_found");
                }
                
                var builderType = builderTypes.First();
                var analysis = AnalyzeOdinGroups(builderType);
                
                McpLogger.LogInfo($"[Builder Browser Automation] Analyzed {builderType.Name}: {analysis.tabGroupCount} TabGroups, {analysis.totalGroups} total groups");
                
                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["builderType"] = builderType.Name,
                    ["analysis"] = JObject.FromObject(analysis),
                    ["message"] = $"✅ Successfully analyzed {builderType.Name}\n" +
                                 $"TabGroups: {analysis.tabGroupCount}\n" +
                                 $"FoldoutGroups: {analysis.foldoutGroupCount}\n" +
                                 $"BoxGroups: {analysis.boxGroupCount}\n" +
                                 $"Total Properties: {analysis.totalProperties}"
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"Analysis failed: {ex.Message}", "analysis_error");
            }
        }
        
        private JObject ListAllBuilders()
        {
            try
            {
                var builderTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(assembly => assembly.GetTypes())
                    .Where(type => type.Name.EndsWith("_Builder") && !type.IsAbstract)
                    .OrderBy(type => type.Name)
                    .Take(20) // Limit for performance
                    .Select(type => type.Name)
                    .ToList();
                
                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["builders"] = JArray.FromObject(builderTypes),
                    ["count"] = builderTypes.Count,
                    ["message"] = $"✅ Found {builderTypes.Count} builder types (showing first 20)"
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"Failed to list builders: {ex.Message}", "reflection_error");
            }
        }
        
        private JObject TestTabGroupDetection()
        {
            try
            {
                // Test the key builders we know have TabGroup issues
                string[] testBuilders = { "OK_Visible_Object_Builder", "OK_Client_Builder", "OK_Floor_Pedestal_Builder" };
                var results = new JArray();
                
                foreach (string builderName in testBuilders)
                {
                    var builderType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(assembly => assembly.GetTypes())
                        .FirstOrDefault(type => type.Name == builderName);
                    
                    if (builderType != null)
                    {
                        var analysis = AnalyzeOdinGroups(builderType);
                        results.Add(new JObject
                        {
                            ["builderName"] = builderName,
                            ["tabGroupCount"] = analysis.tabGroupCount,
                            ["hasVisibleObjectTab"] = analysis.groupPaths.Contains("RootTabGroup/Visible Object"),
                            ["hasClientTab"] = analysis.groupPaths.Contains("RootTabGroup/Client"),
                            ["totalGroups"] = analysis.totalGroups
                        });
                    }
                }
                
                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["testResults"] = results,
                    ["message"] = $"✅ TabGroup detection test completed for {testBuilders.Length} builders"
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"TabGroup test failed: {ex.Message}", "test_error");
            }
        }
        
        // Simplified version of the Odin analysis from BuilderBrowserViewModel
        private (int tabGroupCount, int foldoutGroupCount, int boxGroupCount, int totalGroups, int totalProperties, string[] groupPaths) AnalyzeOdinGroups(Type builderType)
        {
            var tabGroups = 0;
            var foldoutGroups = 0;
            var boxGroups = 0;
            var allPaths = new System.Collections.Generic.HashSet<string>();
            
            var properties = builderType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var fields = builderType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Analyze properties
            foreach (var prop in properties)
            {
                AnalyzeMemberGroups(prop, allPaths, ref tabGroups, ref foldoutGroups, ref boxGroups);
            }
            
            // Analyze fields  
            foreach (var field in fields)
            {
                AnalyzeMemberGroups(field, allPaths, ref tabGroups, ref foldoutGroups, ref boxGroups);
            }
            
            return (tabGroups, foldoutGroups, boxGroups, allPaths.Count, properties.Length + fields.Length, allPaths.ToArray());
        }
        
        private void AnalyzeMemberGroups(MemberInfo member, System.Collections.Generic.HashSet<string> allPaths, ref int tabGroups, ref int foldoutGroups, ref int boxGroups)
        {
            var attributes = member.GetCustomAttributes(true);
            
            foreach (var attr in attributes)
            {
                var attrType = attr.GetType();
                
                // Check for TabGroup
                if (attrType.Name == "TabGroupAttribute")
                {
                    var groupIdProp = attrType.GetProperty("GroupID");
                    var tabIdProp = attrType.GetProperty("TabId");
                    if (groupIdProp != null && tabIdProp != null)
                    {
                        var groupId = groupIdProp.GetValue(attr)?.ToString();
                        var tabId = tabIdProp.GetValue(attr)?.ToString();
                        if (!string.IsNullOrEmpty(groupId) && !string.IsNullOrEmpty(tabId))
                        {
                            var fullPath = $"{groupId}/{tabId}";
                            allPaths.Add(fullPath);
                            tabGroups++;
                        }
                    }
                }
                // Check for FoldoutGroup
                else if (attrType.Name == "FoldoutGroupAttribute")
                {
                    var groupIdProp = attrType.GetProperty("GroupID");
                    if (groupIdProp != null)
                    {
                        var groupId = groupIdProp.GetValue(attr)?.ToString();
                        if (!string.IsNullOrEmpty(groupId))
                        {
                            allPaths.Add(groupId);
                            foldoutGroups++;
                        }
                    }
                }
                // Check for BoxGroup
                else if (attrType.Name == "BoxGroupAttribute")
                {
                    var groupIdProp = attrType.GetProperty("GroupID");
                    if (groupIdProp != null)
                    {
                        var groupId = groupIdProp.GetValue(attr)?.ToString();
                        if (!string.IsNullOrEmpty(groupId))
                        {
                            allPaths.Add(groupId);
                            boxGroups++;
                        }
                    }
                }
            }
        }
    }
}