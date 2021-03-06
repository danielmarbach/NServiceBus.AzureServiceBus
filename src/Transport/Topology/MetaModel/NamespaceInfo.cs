﻿namespace NServiceBus.AzureServiceBus
{
    public class NamespaceInfo
    {
        public NamespaceInfo(string connectionString, NamespaceMode mode = NamespaceMode.Active)
        {
            ConnectionString = connectionString;
            Mode = mode;
        }

        public string ConnectionString { get; set; }

        public NamespaceMode Mode { get; set; }

        protected bool Equals(NamespaceInfo other)
        {
            return string.Equals(ConnectionString, other.ConnectionString); // && Mode == other.Mode; // namespaces can switch mode, so should not be included in the equality check
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != this.GetType())
            {
                return false;
            }
            return Equals((NamespaceInfo)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ConnectionString != null ? ConnectionString.GetHashCode() : 0) * 397); // ^ (int)Mode; // namespaces can switch mode, so should not be included in the equality check
            }
        }

        public static bool operator ==(NamespaceInfo left, NamespaceInfo right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(NamespaceInfo left, NamespaceInfo right)
        {
            return !(left == right);
        }
    }
}