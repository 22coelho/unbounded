using UnityEditor;
using UnityEngine;

namespace Unbounded.Editor
{
    /// <summary>
    /// One-shot project setup, run in batch mode:
    /// Unity -batchmode -quit -projectPath . -executeMethod Unbounded.Editor.ProjectScaffolder.Run
    /// </summary>
    public static class ProjectScaffolder
    {
        public static void Run()
        {
            PlayerSettings.productName = "Unbounded";
            MainSceneBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("Scaffold complete.");
        }
    }
}
