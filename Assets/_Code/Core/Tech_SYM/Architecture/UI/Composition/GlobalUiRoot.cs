using Cooked.Contracts;
using UnityEngine;
namespace Cooked.UI
{
    public sealed class GlobalUiRoot : MonoBehaviour
    {
        [SerializeField] private OptionsView optionsView;
        [SerializeField] private FadeView fadeView;
        private OptionsViewModel optionsModel; private FadeViewModel fadeModel;
        public void Configure(OptionsView options, FadeView fade) { optionsView = options; fadeView = fade; }
        // Services are app-owned and borrowed; this root owns only the two view models.
        public void Bind(OptionsService options, SettingsService settings, FadeService fade)
        {
            Unbind(); optionsModel = new OptionsViewModel(options, settings); fadeModel = new FadeViewModel(fade);
            optionsView.Bind(optionsModel); fadeView.Bind(fadeModel);
        }
        public void Unbind()
        {
            if (optionsView != null) optionsView.Unbind(); if (fadeView != null) fadeView.Unbind();
            optionsModel?.Dispose(); fadeModel?.Dispose(); optionsModel = null; fadeModel = null;
        }
        private void OnDestroy() => Unbind();
    }
}
