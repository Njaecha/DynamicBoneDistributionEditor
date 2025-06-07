using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DynamicBoneDistributionEditor
{
    public struct EditableValue<T>
    {
        private T _orgValue;
        private T _value;

        public T value
        {
            get => IsEdited ? this._value : this._orgValue;
            set
            {
                this.IsEdited = true;
                // maybe add custom comparison for the used types
                if (_orgValue.Equals(value)) this.IsEdited = false;
                if (_orgValue is Keyframe[] orgKeys && value is Keyframe[] newKeys && orgKeys.SequenceEqual(newKeys)) IsEdited = false;
                if (_orgValue is Transform[] orgT && value is Transform[] newT && orgT.SequenceEqual(newT)) IsEdited = false;
                this._value = value;
            }
        }

        public T initialValue => _orgValue;

        public bool IsEdited { get; private set; }

        public EditableValue(T v)
        {
            this._orgValue = v;
            this._value = v;
            this.IsEdited = false;
        }

        public EditableValue(T initalValue, T actualValue)
        {
            this._orgValue = initalValue;
            this._value = actualValue;
            this.IsEdited = true;
            this.value = actualValue; // use value setter to set _edited correctly
        }

        public void Reset()
        {
            this.IsEdited = false;
        }

        public void SetCurrentAsInitial()
        {
            this._orgValue = this._value;
            this.IsEdited = false;
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