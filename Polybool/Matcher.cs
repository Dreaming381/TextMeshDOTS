using System;

namespace TextMeshDOTS.Polybool
{    
    public struct Matcher : IEquatable<Matcher>
    {
        public int index;
        public bool matchesHead;
        public bool matchesPt1;

        public static Matcher Empty => new Matcher { index = -1, matchesHead = false, matchesPt1 = false };

        public Matcher(Matcher matcher)
        {
            this.index = matcher.index;
            this.matchesHead = matcher.matchesHead;
            this.matchesPt1 = matcher.matchesPt1;
        }
        public Matcher(int index, bool matchesHead, bool matchesPt1)
        {
            this.index = index;
            this.matchesHead = matchesHead;
            this.matchesPt1 = matchesPt1;
        }

        public override bool Equals(object obj) => obj is Matcher other && Equals(other);

        public bool Equals(Matcher other)
        {
            return GetHashCode() == other.GetHashCode();
        }

        public static bool operator ==(Matcher e1, Matcher e2)
        {
            return e1.GetHashCode() == e2.GetHashCode();
        }
        public static bool operator !=(Matcher e1, Matcher e2)
        {
            return e1.GetHashCode() != e2.GetHashCode();
        }
        public override int GetHashCode()
        {
            int hashCode = 2055808453;
            hashCode = hashCode * -1521134295 + index.GetHashCode();
            hashCode = hashCode * -1521134295 + matchesHead.GetHashCode();
            hashCode = hashCode * -1521134295 + matchesPt1.GetHashCode();
            return hashCode;
        }
    }
}