using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ADV.Commands.Base;
using JetBrains.Annotations;

namespace DynamicBoneDistributionEditor
{
    public class EditableList<T> : IList<T>
    {
        private readonly List<T> _list;
        private readonly List<T> _orgList;
        
        public List<T> OrgList => _orgList;
        
        public bool IsEdited { get; private set; }

        private void IsEditedCheck()
        {
            IsEdited = false;
            if (_list.Count != _orgList.Count) IsEdited = true;
            else if (!_orgList.SequenceEqual(_list)) IsEdited = true;
        }

        public EditableList()
        {
            _list = new List<T>();
            _orgList = new List<T>();
            IsEdited = false;
        }
        
        public EditableList(T item)
        {
            _list = new List<T> {item};
            _orgList = new List<T> {item};
            IsEdited = false;
        }
        
        public EditableList(params T[] items)
        {
            _list = new List<T>(items);
            _orgList = new List<T>(items);
            IsEdited = false;
        }

        public EditableList(IEnumerable<T> items)
        {
            IEnumerable<T> collection = items as T[] ?? items.ToArray();
            _list = new List<T>(collection);
            _orgList = new List<T>(collection);
            IsEdited = false;
        }

        public EditableList(IEnumerable<T> items, IEnumerable<T> initialItems)
        {
            _list = new List<T>(items);
            _orgList = new List<T>(initialItems);
            IsEdited = true;
            IsEditedCheck();
        }
        
        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            _list.Add(item);
            IsEditedCheck();
        }

        public void Clear()
        {
            _list.Clear();
            IsEditedCheck();
        }

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            bool remove = _list.Remove(item);
            IsEditedCheck();
            return remove;
        }

        public int Count => _list.Count;
        public bool IsReadOnly => false;

        public int IndexOf(T item)
        {
            return _list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            _list.Insert(index, item);
            IsEditedCheck();
        }

        public void RemoveAt(int index)
        {
            _list.RemoveAt(index);
            IsEditedCheck();
        }

        public T this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }

        public void Reset()
        {
            _list.Clear();
            _list.AddRange(_orgList);
            IsEdited = false;
        }

        public bool ContainsOriginal(T item)
        {
            return _orgList.Contains(item);
        }

        public static explicit operator List<T>(EditableList<T> value)
        {
            return value._list;
        }

        public void SetCurrentAsInitial()
        {
            _orgList.Clear();
            _orgList.AddRange(_list);
            IsEdited = false;
        }
    }
}