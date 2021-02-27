using System;

public class ResizableArray<T> {
    private T[] _array;
    private int _count = 0;

    
    public ResizableArray(int initialSize = 10) {
        _array = new T[initialSize];
    }

    public int Count {
        get { return _count; }
    }

    public void Add(T element) {
        if (_count == _array.Length) {
            Array.Resize(ref _array, _array.Length * 2);
        }

        _array[_count++] = element;
    }

    public T[] GetArrayRef() {
        return _array;
    }

    public void Clear() {
        _count = 0;
    }

    public void SetArray(T[] array) {
        _count = array.Length;
        _array = array;
    }
}