using System;
using System.IO;
using UnityEditor;

namespace FishingEditor
{
    // Explicit, local development request only; never runs a scene builder or saves scenes.
    [InitializeOnLoad]
    public static class PlayerEditorRefresh
    {
        const string Request="tools/PlayerEditorRefresh.request";
        static PlayerEditorRefresh() { EditorApplication.update+=Watch; }
        static void Watch()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            File.Move(Request,Request+".done-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            EditorApplication.delayCall+=()=>AssetDatabase.Refresh();
        }
    }
}
