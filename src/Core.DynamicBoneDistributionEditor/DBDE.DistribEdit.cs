using System;
using UnityEngine;

namespace DynamicBoneDistributionEditor
{
    public class DBDEDistribEdit
    {
        private Keyframe[] changedKeyframes;
        private readonly Keyframe[] oldKeyframes;
        public bool HasEdits { get => !changedKeyframes.IsNullOrEmpty(); }

        public DBDEDistribEdit(Keyframe[] initialKeyframes)
        {
            this.oldKeyframes = initialKeyframes;
        }

        public Keyframe[] GetKeyframes()
        {
            if (changedKeyframes.IsNullOrEmpty()) return oldKeyframes;
            else return changedKeyframes;
        }

        public Keyframe[] GetInitialKeyframes()
        {
            return oldKeyframes;
        }

        public void SetKeyframes(Keyframe[] keyframes)
        {
            changedKeyframes = new Keyframe[keyframes.Length];
            changedKeyframes = keyframes;
        }

        internal void Reset()
        {
            changedKeyframes = null;
        }
    }
}


