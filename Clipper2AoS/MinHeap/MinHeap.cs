using System;
using Unity.Collections;
using Unity.Jobs;

namespace ClipperAoS.MinHeap
{
    public enum Comparison
    {
        Min,
        Max
    };
    public struct MinHeap<T> : IDisposable where T : unmanaged, IComparable<T>
    {
        public NativeList<T> _stack;
        int m_CompareMultiplier;
        public int Count { get { return _stack.Length; } }
        public bool IsCreated { get { return _stack.IsCreated; } }
        public bool IsEmpty { get { return _stack.Length == 0; } }

        public void Clear()
        {
            _stack.Clear();
        }
        public MinHeap(int size, Allocator _allocator, Comparison comparison = Comparison.Min)
        {
            _stack = new NativeList<T>(size, _allocator);//needed size depends on precision
            m_CompareMultiplier = (comparison == Comparison.Min) ? 1 : -1;
        }
        public void Push(T value)
        {
            _stack.Add(value);
            BubbleUp(_stack.Length - 1);
        }
        public T Pop()
        {
            T result = default;
            if (!IsEmpty)
            {
                result = _stack[0];
                DeleteRoot();
            }
            return result;
        }
        public T Peek()
        {
            T result = default;
            if (!IsEmpty)
            {
                result = _stack[0];
            }
            return result;
        }
        public void BubbleUp(int childIndex)
        {
            while (childIndex > 0)
            {
                int parentIndex = (childIndex - 1) / 2;
                ref var child = ref _stack.ElementAt(childIndex);
                ref var parent = ref _stack.ElementAt(parentIndex);
                if (child.CompareTo(parent) * m_CompareMultiplier < 0)
                {
                    (child, parent) = (parent, child);
                    childIndex = parentIndex;
                }
                else break;
            }
        }

        public void DeleteRoot()
        {
            if (_stack.Length <= 1)
            {
                _stack.Clear();
                return;
            }
            _stack.RemoveAtSwapBack(0);   //using _stack.RemoveAt(0) would destroy parent child relationships of heap, so RemoveAtSwapBack(0) is essential!
            BubbleDown(0);
        }
        public void BubbleDown(int index)
        {
            var length = _stack.Length;
            var halfLength = length / 2;
            while (index < halfLength)
            {
                // on each iteration exchange element with its smallest child
                var doubleIndex = index * 2;
                int leftChildIndex = doubleIndex + 1;
                int rightChildIndex = doubleIndex + 2;

                if (leftChildIndex < length)
                {
                    ref var leftChild = ref _stack.ElementAt(leftChildIndex);
                    ref var parentItem = ref _stack.ElementAt(index);
                    if (leftChild.CompareTo(parentItem) * m_CompareMultiplier < 0)
                    {
                        //left is smaller then parent, check if right is even smaller
                        if (rightChildIndex < length)
                        {
                            ref var rightChild = ref _stack.ElementAt(rightChildIndex);
                            if (rightChild.CompareTo(leftChild) * m_CompareMultiplier < 0)
                            {
                                //right is even smaller
                                (rightChild, parentItem) = (parentItem, rightChild);
                                index = rightChildIndex;
                                continue;
                            }
                        }
                        //no, left is smallest 
                        (leftChild, parentItem) = (parentItem, leftChild);
                        index = leftChildIndex;
                        continue;
                    }
                }
                if (rightChildIndex < length)
                {
                    ref var rightChild = ref _stack.ElementAt(rightChildIndex);
                    ref var parentItem = ref _stack.ElementAt(index);
                    if (rightChild.CompareTo(parentItem) * m_CompareMultiplier < 0)
                    {
                        (rightChild, parentItem) = (parentItem, rightChild);
                        index = rightChildIndex;
                        continue;
                    }
                }
                break;
            }
        }
        public void Dispose()
        {
            _stack.Dispose();
        }
        public void Dispose(JobHandle jobHandle)
        {
            _stack.Dispose(jobHandle);
        }
    }
}
