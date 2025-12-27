using System;
using System.Diagnostics;

namespace TextMeshDOTS.Polybool
{
	[DebuggerDisplay("{segmentID} {isStart}")]
	public struct EventBool : IEquatable<EventBool>
    {        
        public int segmentID;
		byte _boolField;

		//// why not just use bools to store isStart?
		//// because it makes GetHashCode faster 
		//// GetHashCode is used when searching events via eventQueue.IndexOf(event)
		//public bool isSubject
		//{
		//	get { return Utils.GetBit(_boolField, 1); }
		//	set { _boolField = Utils.SetBit(_boolField, 1, value); }
		//}
		public bool isStart
		{
			get { return Utils.GetBit(_boolField, 2); }
			set { _boolField = Utils.SetBit(_boolField, 2, value); }
		}

		public EventBool other => new EventBool (!isStart, segmentID);
        public static EventBool Empty => new EventBool(false, -1);

        public EventBool(bool isStart, int segmentID)
        {
			_boolField = 0;			
            this.segmentID = segmentID;
			this.isStart = isStart;
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
			return HashCode.Combine(segmentID, _boolField);
		}
	}
}