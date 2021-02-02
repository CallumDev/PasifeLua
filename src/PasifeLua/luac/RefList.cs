using System;
using System.Runtime.CompilerServices;

namespace PasifeLua.luac
{
    //Alternative for List<T> that uses ref returns
    //Starts with capacity of 10, doubles in size
    class RefList<T>
    {
        private int n = 0;
        private T[] backing = new T[10];
        private bool withspace = false;
        public RefList() : this(false)
        {
        }

        //withspace disables bound checks
        //done for codegen
        public RefList(bool withspace) 
        {
            this.withspace = withspace;
        }

        public void Add(T item)
        {
            if (backing.Length < n + 1 + (withspace ? 5 : 0))
                Grow();
            backing[n++] = item;
        }

        public void SetOrAdd(int i, T item)
        {
            if (i >= n) n = i + 1;
            if (backing.Length < n + 1 + (withspace ? 5 : 0))
                Grow();
            backing[i] = item;
        }

        public void RemoveEnd(int count)
        {
            ShrinkTo(n - count);
        }

        public void ShrinkTo(int newn)
        {
            for (int i = (n - 1); i > (newn); i++) {
                backing[i] = default;
            }
            n = newn;
        }

        /// <summary>
        /// WARNING: invalidates all current references
        /// </summary>
        /// <param name="index">index to remove at</param>
        public void RemoveAt(int g)
        {
            for (int i = g; i < Count - 1; i++)
                backing[i] = backing[i + 1];
            n--;
        }
        
        public ref T this[int idx]
        {
            get
            {
                if(!withspace && (idx < 0 || idx >= n)) throw new IndexOutOfRangeException();
                return ref backing[idx];
            }
        }

        public int Count => n;

        public T[] ToArray()
        {
            var res = new T[n];
            Array.Copy(backing, res, n);
            return res;
        }

        void Grow()
        {
            int sz = backing.Length <= 0 ? 1 : backing.Length;
            sz = sz << 1; //double in size (first size = 2)
            if (withspace) sz += 5;
            Array.Resize(ref backing, sz);
        }
    }
}