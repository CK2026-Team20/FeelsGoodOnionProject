using System.IO;
using UnityEditor;
using UnityEngine;
namespace Cooked.Dialogue.Editor
{
    public static class DialogueVerificationMenu
    {
        [MenuItem("Cooked/Dialogue/Run Pure Regression Tests")]
        public static void RunPure()
        {
            var results = Tests.DialogueRegressionTests.Run();
            const string directory = "output/Tech_SYM/dialogue/evidence";
            Directory.CreateDirectory(directory);
            File.WriteAllLines(directory + "/unity-pure-tests.txt", results);
            Debug.Log("Dialogue pure tests passed: " + results.Count + ". This does not assert rendering/input/LOOP validation.");
        }
    }
}
