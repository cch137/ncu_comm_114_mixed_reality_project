using System;
using UnityEngine;

namespace EVE
{
    public class SequenceTracker : ScriptableObject
    {
        public SequenceTracker Init(Sequence s, string fp)
        {
            this.s = s;
            this.fp = fp;
            return this;
        }
        public Sequence s;
        public string fp;
    }
}