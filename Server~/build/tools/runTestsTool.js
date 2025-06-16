import * as z from 'zod';
import { McpUnityError, ErrorType } from '../utils/errors.js';
// Constants for the tool
const toolName = 'run_tests';
const toolDescription = 'Runs Unity\'s Test Runner tests';
const paramsSchema = z.object({
    testMode: z.string().optional().default('EditMode').describe('The test mode to run (EditMode or PlayMode) - defaults to EditMode (optional)'),
    testFilter: z.string().optional().default('').describe('The specific test filter to run (e.g. specific test name or namespace) (optional)'),
    returnOnlyFailures: z.boolean().optional().default(true).describe('Whether to show only failed tests in the results (optional)')
});
/**
 * Creates and registers the Run Tests tool with the MCP server
 * This tool allows running tests in the Unity Test Runner
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerRunTestsTool(server, mcpUnity, logger) {
    logger.info(`Registering tool: ${toolName}`);
    // Register this tool with the MCP server
    server.tool(toolName, toolDescription, paramsSchema.shape, async (params = {}) => {
        try {
            logger.info(`Executing tool: ${toolName}`, params);
            const result = await toolHandler(mcpUnity, params);
            logger.info(`Tool execution successful: ${toolName}`);
            return result;
        }
        catch (error) {
            logger.error(`Tool execution failed: ${toolName}`, error);
            throw error;
        }
    });
}
/**
 * Handles running tests in Unity
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(mcpUnity, params = {}) {
    const { testMode = 'EditMode', testFilter = '', returnOnlyFailures = true } = params;
    // Create and wait for the test run
    const response = await mcpUnity.sendRequest({
        method: toolName,
        params: {
            testMode,
            testFilter,
            returnOnlyFailures
        }
    });
    // Process the test results
    if (!response.success) {
        throw new McpUnityError(ErrorType.TOOL_EXECUTION, response.message || `Failed to run tests: Mode=${testMode}, Filter=${testFilter || 'none'}`);
    }
    // Extract test results
    const testResults = response.results || [];
    const testCount = response.testCount || 0;
    const passCount = response.passCount || 0;
    const failCount = response.failCount || 0;
    const skipCount = response.skipCount || 0;
    return {
        content: [
            {
                type: 'text',
                text: response.message
            },
            {
                type: 'text',
                text: JSON.stringify({
                    testCount,
                    passCount,
                    failCount,
                    skipCount,
                    results: testResults
                }, null, 2)
            }
        ]
    };
}
