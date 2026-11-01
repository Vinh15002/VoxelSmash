using UnityEditor;
using UnityEditor.Compilation;

namespace VoxelSmash.EditorTools
{
    // Claude Code launches the MCP for Unity server over stdio (see ~/.claude.json "UnityMCP"), so the
    // editor side must run the stdio bridge instead of the package's default HTTP transport. Applied
    // once per machine; change it later from Window > MCP for Unity if needed.
    [InitializeOnLoad]
    static class McpTransportSetup
    {
        const string HttpKey = "MCPForUnity.UseHttpTransport", AppliedKey = "VoxelSmash.McpStdioApplied";

        static McpTransportSetup()
        {
            if (EditorPrefs.GetBool(AppliedKey, false)) return;
            EditorPrefs.SetBool(HttpKey, false);
            EditorPrefs.SetBool(AppliedKey, true);
            // The bridge reads the transport while the domain loads; reload once so it starts on stdio.
            CompilationPipeline.RequestScriptCompilation();
        }
    }
}
