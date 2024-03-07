using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DynamicBoneDistributionEditor
{
    public struct EditableValue<T>
    {
        private readonly T _orgValue;
        private T _value;
        private bool _edited;

        public T value
        {
            get
            {
                if (_edited) return this._value;
                else return this._orgValue;
            }
            set
            {
                this._edited = true;
                // maybe add custom comparison for the used types
                if (_orgValue.Equals(value)) this._edited = false;
                if (_orgValue is Keyframe[] orgKeys && value is Keyframe[] newKeys && orgKeys.SequenceEqual(newKeys)) _edited = false;
                this._value = value;
            }
        }

        public T initialValue { get => _orgValue; }

        public bool IsEdited { get => _edited; }

        public EditableValue(T v)
        {
            this._orgValue = v;
            this._value = v;
            this._edited = false;
        }

        public void Reset()
        {
            this._edited = false;
        }

        public static implicit operator T(EditableValue<T> v)
        {
            return v.value;
        }

        public static explicit operator EditableValue<T>(T v)
        {
            return new EditableValue<T>(v);
        }
    }
}