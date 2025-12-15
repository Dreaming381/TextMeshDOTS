using System;

namespace TextMeshDOTS.Polybool
{
    public struct EventBool : IEquatable<EventBool>
    {
        public bool isStart;
        public int segmentID;
        public bool primary;
        public EventBool other => new EventBool (!isStart, segmentID, primary);
        public static EventBool Empty => new EventBool(false, -1, false);

        public EventBool(bool isStart, int segmentID, bool primary)
        {
            this.isStart = isStart;
            this.segmentID = segmentID;
            this.primary = primary;
        }

        public override bool Equals(object obj) => obj is EventBool other && Equals(other);

        public bool Equals(EventBool other)
        {
            return other != null && GetHashCode() == other.GetHashCode();
        }

        public static bool operator ==(EventBool e1, EventBool e2)
        {
            return e1.GetHashCode() == e2.GetHashCode();
        }
        public static bool operator !=(EventBool e1, EventBool e2)
        {
            return !(e1==e2);
        }
        public override int GetHashCode()
        {
            int hashCode = 2055808453;
            hashCode = hashCode * -1521134295 + isStart.GetHashCode();
            hashCode = hashCode * -1521134295 + segmentID.GetHashCode();
            hashCode = hashCode * -1521134295 + primary.GetHashCode();
            return hashCode;
        }
    }
}