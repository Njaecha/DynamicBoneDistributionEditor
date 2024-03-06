using System;
using System.Collections.Generic;

namespace DynamicBoneDistributionEditor
{
    public struct EditableValue<T>
    {
        private T _orgValue;
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
                // maybe custom comparison for the used types
                if (_orgValue.Equals(value)) return;
                this._value = value;
                this._edited = true;
            }
        }

        public bool IsEdited { get => IsEdited; }

        public EditableValue(T v)
        {
            this._orgValue = v;
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

        public static implicit operator EditableValue<T>(T v)
        {
            return new EditableValue<T>(v);
        }
    }
}