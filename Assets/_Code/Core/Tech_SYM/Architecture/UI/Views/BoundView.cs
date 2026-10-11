using System.ComponentModel;
using System;
using UnityEngine;
namespace Cooked.UI
{
    public abstract class BoundView<T> : MonoBehaviour where T : ObservableViewModel
    {
        protected T Model { get; private set; }
        private bool connected;
        public void Bind(T model)
        {
            if (model != null && model.IsDisposed) throw new ObjectDisposedException(nameof(model));
            if (model != null) ValidateReferences();
            if (ReferenceEquals(Model, model)) { if (isActiveAndEnabled) Connect(); Refresh(); return; }
            Unbind(); Model = model;
            try { if (isActiveAndEnabled) Connect(); Refresh(); }
            catch (Exception error)
            {
                try { Unbind(); }
                catch (Exception cleanup) { throw new AggregateException("View binding and cleanup failed.", error, cleanup); }
                throw;
            }
        }
        public void Unbind() { try { Disconnect(); } finally { Model = null; Refresh(); } }
        private void Connect()
        {
            if (connected || Model == null || Model.IsDisposed) return;
            ValidateReferences();
            connected = true; Model.PropertyChanged += OnChanged;
            try { ConnectInputs(); }
            catch (Exception error)
            {
                try { Disconnect(); }
                catch (Exception cleanup) { throw new AggregateException("View input binding and cleanup failed.", error, cleanup); }
                throw;
            }
        }
        private void Disconnect()
        {
            if (!connected) return;
            connected = false;
            try { DisconnectInputs(); }
            finally { if (Model != null) Model.PropertyChanged -= OnChanged; }
        }
        private void OnChanged(object sender, PropertyChangedEventArgs args) => Refresh();
        protected virtual void OnEnable() { Connect(); Refresh(); }
        protected virtual void OnDisable() => Disconnect();
        protected virtual void OnDestroy() => Unbind();
        protected virtual void ConnectInputs() { }
        protected virtual void ValidateReferences() { }
        protected virtual void DisconnectInputs() { }
        protected abstract void Refresh();
    }
}
