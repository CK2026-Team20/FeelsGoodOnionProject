using System;
using UnityEngine;
namespace Cooked.UI.Editor
{
    public static class PrototypeUiAuthoring
    {
        [Obsolete("Primary UiPrefabBuilder authoring includes the approved layout; no correction pass is required.")]
        public static void Apply() => UiPrefabBuilder.BuildAll();
        public static void ConfigureTitle(GameObject root) => UiPrefabBuilder.ConfigureTitle(root);
    }
}
