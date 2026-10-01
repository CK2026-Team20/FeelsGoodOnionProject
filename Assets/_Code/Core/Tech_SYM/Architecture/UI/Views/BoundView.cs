using System.ComponentModel;
using UnityEngine;
namespace Cooked.UI
{
    public abstract class BoundView<T> : MonoBehaviour where T : ObservableViewModel
    {
        protected T Model { get; private set; }
        private bool connected;
        public void Bind(T model)
        { Unbind(); Model = model; if (isActiveAndEnabled) Connect(); Refresh(); }
        public void Unbind() { Disconnect(); Model = null; Refresh(); }
        private void Connect()
        { if (connected || Model == null || Model.IsDisposed) return; connected = true; Model.PropertyChanged += OnChanged; ConnectInputs(); }
        private void Disconnect()
        { if (!connected) return; DisconnectInputs(); if (Model != null) Model.PropertyChanged -= OnChanged; connected = false; }
        private void OnChanged(object sender, PropertyChangedEventArgs args) => Refresh();
        protected virtual void OnEnable() { Connect(); Refresh(); }
        protected virtual void OnDisable() => Disconnect();
        protected virtual void OnDestroy() => Unbind();
        protected virtual void ConnectInputs() { }
        protected virtual void DisconnectInputs() { }
        protected abstract void Refresh();
    }
}
