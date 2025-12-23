using System;
using System.Runtime.CompilerServices;

namespace TextMeshDOTS.Polybool
{
    public struct EventBool : IEquatable<EventBool>
    {
        public int segmentID;		
		byte _boolField;

		// why not just use bools to store primary and isStart?
		// because it makes GetHashCode faster 
		// GetHashCode is used when searching events via eventQueue.IndexOf(event)
		public bool primary
		{
			get { return GetBit(_boolField, 1); }
			set { _boolField = SetBit(_boolField, 1, value); }
		}
		public bool isStart
		{
			get { return GetBit(_boolField, 2); }
			set {_boolField = SetBit(_boolField, 2, value);}
		}
		public EventBool other => new EventBool (!isStart, segmentID, primary);
        public static EventBool Empty => new EventBool(false, -1, false);

        public EventBool(bool isStart, int segmentID, bool primary)
        {
			_boolField = 0;
            this.segmentID = segmentID;
			this.isStart = isStart;
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
			hashCode = hashCode * -1521134295 + _boolField;
			hashCode = hashCode * -1521134295 + segmentID;
			return hashCode;
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static byte SetBit(byte value, int bitIndex, bool flag)
		{
			if (flag)
				return (byte)(value | (1 << bitIndex));   // set bit
			else
				return (byte)(value & ~(1 << bitIndex));  // clear bit
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool GetBit(byte value, int bitIndex)
		{
			return (value & (1 << bitIndex)) != 0;
		}
		/// <summary>
		/// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
		/// </summary>

	}
}