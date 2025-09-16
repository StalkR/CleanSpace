using System;
using System.ComponentModel;

namespace CleanSpaceShared.Struct
{
    [Serializable]
    public class PluginListEntry : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _name;
        private string _assemblyName;
        private string _version;
        private string _location;
        private string _hash;
        private DateTime _lastHashed;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public int IsCleanSpace => _assemblyName.Contains("CleanSpace") ? 1 : 0;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string AssemblyName
        {
            get => _assemblyName;
            set
            {
                if (_assemblyName != value)
                {
                    _assemblyName = value;
                    OnPropertyChanged(nameof(AssemblyName));
                }
            }
        }

        public string Version
        {
            get => _version;
            set
            {
                if (_version != value)
                {
                    _version = value;
                    OnPropertyChanged(nameof(Version));
                }
            }
        }

        public string Hash
        {
            get => _hash;
            set
            {
                if (_hash != value)
                {
                    _hash = value;
                    OnPropertyChanged(nameof(Hash));
                }
            }
        }

        public DateTime LastHashed
        {
            get => _lastHashed;
            set
            {
                if (_lastHashed != value)
                {
                    _lastHashed = value;
                    OnPropertyChanged(nameof(LastHashed));
                }
            }
        }

        public string Location 
        { 
            get => _location;
            set
            {
                _location = value;
               OnPropertyChanged(nameof(Location));
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is PluginListEntry other))
                return false;

            return Hash == other.Hash &&
                   Version == other.Version &&
                   AssemblyName == other.AssemblyName;
        }

        public override int GetHashCode()
        {
            return Hash?.GetHashCode() ?? 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
