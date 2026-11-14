using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Boids
{
    public sealed class BoidPool : IDisposable
    {
        readonly BoidAgent prefab;
        readonly Transform root;
        readonly BoidAnimationBatch animations;
        bool disposed;
        readonly Stack<BoidAgent> free = new Stack<BoidAgent>();
        readonly HashSet<BoidAgent> leased = new HashSet<BoidAgent>();
        public int Created { get; private set; }

        public int InUse => leased.Count;
        public BoidPool(BoidAgent source, Transform parent, int capacity)
        {
            prefab = source;
            root = parent;
            animations = parent.gameObject.AddComponent<BoidAnimationBatch>();
            for (int i = 0; i < capacity; i++)
                free.Push(Create());
        }

        BoidAgent Create()
        {
            var a = UnityEngine.Object.Instantiate(prefab, root);
            a.gameObject.SetActive(false);
            Created++;
            return a;
        }

        public BoidAgent Rent(Vector3 p, Vector3 v, float phase)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(BoidPool));
            var a = free.Count > 0 ? free.Pop() : Create();
            if (!leased.Add(a))
                throw new InvalidOperationException("보이드 중복 대여");
            a.Acquire(p, v, phase, animations);
            return a;
        }

        public void Return(BoidAgent a)
        {
            if (!leased.Remove(a))
                throw new InvalidOperationException("보이드 중복 반환 또는 다른 풀의 개체");
            a.Release();
            free.Push(a);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            foreach (var a in leased)
                if (a)
                {
                    a.Release();
                    UnityEngine.Object.Destroy(a.gameObject);
                }

            foreach (var a in free)
                if (a)
                    UnityEngine.Object.Destroy(a.gameObject);
            leased.Clear();
            free.Clear();
            if (animations)
            {
                animations.Shutdown();
                UnityEngine.Object.Destroy(animations);
            }
        }

        public static float[] StratifiedPhases(int count, System.Random random)
        {
            var values = new float[count];
            for (int i = 0; i < count; i++)
                values[i] = (i + (float)random.NextDouble()) / count;
            for (int i = count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }

            return values;
        }
    }
}
