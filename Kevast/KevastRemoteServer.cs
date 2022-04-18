using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kevast
{
    public class KevastRemoteServer : IComparable, IComparable<KevastRemoteServer>, IEquatable<KevastRemoteServer>
    {
        public KevastRemoteServer(Uri baseUri)
        {
            if (baseUri == null)
                throw new ArgumentNullException(nameof(baseUri));

            BaseUri = baseUri;
            State = KevastRemoteServerState.Disconnected;
        }

        public Uri BaseUri { get; }
        public Guid Id { get; set; }
        public KevastRemoteServerState State { get; set; }

        public override string ToString() => BaseUri.ToString();
        int IComparable.CompareTo(object? obj) => CompareTo(obj as KevastRemoteServer);
        public int CompareTo(KevastRemoteServer? other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            return BaseUri.ToString().CompareTo(other.BaseUri.ToString());
        }

        public override int GetHashCode() => BaseUri.GetHashCode();
        public override bool Equals(object? obj) => Equals(obj as KevastRemoteServer);
        public bool Equals(KevastRemoteServer? other) => other != null && BaseUri.Equals(other.BaseUri);

    }
}
