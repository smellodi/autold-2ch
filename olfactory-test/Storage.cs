using System;

namespace Olfactory
{
    /// <summary>
    /// Cross-app storage, mainly used to share the app state
    /// </summary>
    public class Storage
    {
        // Instance
        public static Storage Instance => _instance = _instance ?? new();

        // Event

        public enum Data
        {
            IsDebugging,
        }

        public event EventHandler<Data> Changed = delegate { };

        // Variable

        public bool IsDebugging
        {
            get => _isDebugging;
            set
            {
                _isDebugging = value;
                Changed(this, Data.IsDebugging);
            }
        }

        // Internal data

        static Storage _instance;

        bool _isDebugging = false;

        private Storage() { }
    }
}
