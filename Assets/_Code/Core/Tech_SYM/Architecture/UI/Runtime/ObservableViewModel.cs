using System;
using System.ComponentModel;
namespace Cooked.UI
{
    public abstract class ObservableViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public bool IsDisposed { get; private set; }
        protected void Notify(string name = "") { if (!IsDisposed) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
        public void Dispose() { if (IsDisposed) return; IsDisposed = true; Release(); PropertyChanged = null; }
        protected virtual void Release() { }
    }
}
