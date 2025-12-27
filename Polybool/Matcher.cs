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
            index = matcher.index;
            matchesHead = matcher.matchesHead;
            matchesPt1 = matcher.matchesPt1;
        }
        public Matcher(int index, bool matchesHead, bool matchesPt1)
        {
            this.index = index;
            this.matchesHead = matchesHead;
            this.matchesPt1 = matchesPt1;
        }

        public override bool Equals(object obj)
        {
            if (obj != null && obj is Matcher p)
                return this == p;
            else
                return false;
        }

        public bool Equals(Matcher other)
        {
            return GetHashCode() == other.GetHashCode();
        }

        public static bool operator ==(Matcher e1, Matcher e2)
        {
            return e1.index == e2.index && e1.matchesHead == e2.matchesHead && e1.matchesPt1 == e2.matchesPt1;
        }
        public static bool operator !=(Matcher e1, Matcher e2)
        {
            return e1.index != e2.index && e1.matchesHead != e2.matchesHead && e1.matchesPt1 != e2.matchesPt1;
        }
        public readonly override int GetHashCode()
        {
            return HashCode.Combine(index, matchesHead, matchesPt1);
        }
    }
}