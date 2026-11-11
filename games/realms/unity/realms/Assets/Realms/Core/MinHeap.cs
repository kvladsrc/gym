using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>A binary min-heap; equal keys pop in insertion order, so runs repeat exactly.</summary>
    internal sealed class MinHeap
    {
        readonly List<float> keys = new List<float>();
        readonly List<long> order = new List<long>();
        readonly List<int> values = new List<int>();
        long counter;

        public int Count => keys.Count;

        bool Less(int a, int b) => keys[a] < keys[b] || (keys[a] == keys[b] && order[a] < order[b]);

        void Swap(int a, int b)
        {
            (keys[a], keys[b]) = (keys[b], keys[a]);
            (order[a], order[b]) = (order[b], order[a]);
            (values[a], values[b]) = (values[b], values[a]);
        }

        public void Push(float key, int value)
        {
            keys.Add(key);
            order.Add(counter++);
            values.Add(value);
            int i = keys.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!Less(i, parent))
                    break;
                Swap(i, parent);
                i = parent;
            }
        }

        public void Pop(out float key, out int value)
        {
            key = keys[0];
            value = values[0];
            int last = keys.Count - 1;
            Swap(0, last);
            keys.RemoveAt(last);
            order.RemoveAt(last);
            values.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int left = 2 * i + 1, right = left + 1, smallest = i;
                if (left < keys.Count && Less(left, smallest))
                    smallest = left;
                if (right < keys.Count && Less(right, smallest))
                    smallest = right;
                if (smallest == i)
                    break;
                Swap(i, smallest);
                i = smallest;
            }
        }
    }
}
