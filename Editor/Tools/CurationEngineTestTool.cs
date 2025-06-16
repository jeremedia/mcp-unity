using System;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Test tool for Curation Engine integration - validates MCP Unity fork permissions
    /// </summary>
    public class CurationEngineTestTool : McpToolBase
    {
        public CurationEngineTestTool()
        {
            Name = "curation_engine_test";
            Description = "Test tool for Curation Engine MCP Unity fork - confirms write permissions and basic functionality";
        }
        
        /// <summary>
        /// Execute the test tool to confirm Curation Engine integration
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            string testMessage = parameters["message"]?.ToObject<string>() ?? "🎯 Curation Engine MCP Unity Fork - Write Permissions Confirmed!";
            
            // Log success message to Unity Console
            McpLogger.LogInfo($"[CE MCP Test] {testMessage}");
            
            // Get some basic Unity Editor info for validation
            string unityVersion = Application.unityVersion;
            string projectName = Application.productName;
            bool isPlaying = Application.isPlaying;
            
            // Create detailed response
            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"✅ Curation Engine MCP Unity Fork Test Successful!\n" +
                            $"Unity Version: {unityVersion}\n" +
                            $"Project: {projectName}\n" +
                            $"Is Playing: {isPlaying}\n" +
                            $"Test Message: {testMessage}\n" +
                            $"Ready for Curation Engine tools development! 🚀"
            };
        }
    }
}