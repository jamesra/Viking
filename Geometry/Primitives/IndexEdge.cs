using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    internal struct IndexEdge
    {
        /// <summary>
        /// I need an indicator if the Edge has a valid value
        /// </summary>
        public bool IsValid;

        public readonly int iA;
        public readonly int iB;

        public IndexEdge(int A, int B)
        {
            IsValid = true;

            if (A < B)
            {
                this.iA = A;
                this.iB = B;
            }
            else
            {
                this.iA = B;
                this.iB = A;
            }
        }

        public override int GetHashCode()
        {
            return iA * iB;
        }

        public override string ToString()
        {
            return iA.ToString() + " - " + iB.ToString();
        }

        public static bool operator ==(IndexEdge A, IndexEdge B)
        {
            if (A.IsValid != B.IsValid)
                return false; 

            if (A.iA == B.iA &&
                   A.iB == B.iB)
                return true;

            if (A.iB == B.iA &&
               A.iA == B.iB)
                return true;

            return false;
        }

        public static bool operator !=(IndexEdge A, IndexEdge B)
        {
            if (A.IsValid != B.IsValid)
                return true; 

            if (A.iA == B.iA &&
                   A.iB == B.iB)
                return false;

            if (A.iB == B.iA &&
               A.iA == B.iB)
                return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            IndexEdge E = (IndexEdge)obj;
            if (E != null)
            {
                return this == E;
            }

            return base.Equals(obj);
        }
    }
}
