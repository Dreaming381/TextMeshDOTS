using System;
using System.Diagnostics;

namespace TextMeshDOTS.Polybool
{
    [DebuggerDisplay("{segmentID} {isStart}")]
    public struct EventBool : IEquatable<EventBool>
    {
        public int segmentID;
        ushort _boolField;
       
        public bool isStart
        {
            get { return Utils.GetBit(_boolField, 1); }
            set { _boolField = Utils.SetBit(_boolField, 1, value); }
        }

        public EventBool other => new EventBool(!isStart, segmentID);
        public static EventBool Empty => new EventBool(false, -1);

        public EventBool(bool isStart, int segmentID)
        {
            _boolField = 0;
            this.segmentID = segmentID;
            this.isStart = isStart;
        }

        public override bool Equals(object obj)
        {
            return obj is EventBool other && Equals(other);
        }

        public bool Equals(EventBool other)
        {
            return segmentID == other.segmentID && _boolField == other._boolField;
        }

        public static bool operator ==(EventBool e1, EventBool e2)
        {
            return e1.segmentID == e2.segmentID && e1._boolField == e2._boolField;
        }
        public static bool operator !=(EventBool e1, EventBool e2)
        {
            return !(e1 == e2);
        }
        public override int GetHashCode()
        {
            //return HashCode.Combine(segmentID, _boolField);
            int hashCode = 2055808453;
            hashCode = hashCode * -1521134295 + segmentID;
            hashCode = hashCode * -1521134295 + _boolField;
            return hashCode;
        }
    }
}