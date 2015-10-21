using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmisSync.Lib.Utils
{
    class LinkedListIterator<T>
    {
        public enum InitialPosition
        {
            Start, End
        }

        private LinkedListIterator<T> parentIterator;
        private LinkedList<T> list;

        private LinkedListNode<T> previousNode;
        private LinkedListNode<T> currentNode;
        private LinkedListNode<T> nextNode;

        public LinkedListIterator(LinkedList<T> list, InitialPosition initialPosition = InitialPosition.Start)
        {
            this.list = list;
            switch (initialPosition)
            {
                case InitialPosition.Start:
                    this.nextNode = list.First;
                    break;
                case InitialPosition.End:
                    this.previousNode = list.Last;
                    break;
                default:
                    throw new ArgumentException();
            }
        }

        //clone constructor
        protected LinkedListIterator(LinkedListIterator<T> iterator)
        {
            this.parentIterator = iterator;
            this.list = iterator.list;
            this.previousNode = iterator.previousNode;
            this.currentNode = iterator.currentNode;
            this.nextNode = iterator.nextNode;
        }

        public T Current
        { 
            get
            {
                return currentNode != null ? currentNode.Value : default(T);
            }
            set
            {
                if (currentNode != null)
                {
                    this.currentNode.Value = value;
                }
                else {
                    throw new InvalidOperationException("Current node has not been set or removed.");
                }
            }
        }

        public bool HasPrevious()
        {
            return previousNode != null;
        }

        public T Previous()
        {
            this.currentNode = this.previousNode;
            this.nextNode = this.currentNode.Next;
            this.previousNode = this.currentNode.Previous;
            return currentNode.Value;
        }

        public bool hasNext()
        {
            return this.nextNode != null;
        }

        public T Next()
        {
            this.currentNode = this.nextNode;
            this.nextNode = this.currentNode.Next;
            this.previousNode = this.currentNode.Previous;
            return currentNode.Value;
        }

        public void Remove()
        {
            if (parentIterator != null)
            {
                parentIterator.BeforeChildIteratorRemove(this.currentNode);
            }
            this.list.Remove(this.currentNode);
            this.currentNode = null;
        }

        private void BeforeChildIteratorRemove(LinkedListNode<T> node)
        {
            if (this.parentIterator != null)
            {
                this.parentIterator.BeforeChildIteratorRemove(node);
            }

            if (node == nextNode)
            {
                nextNode = node.Next;
            }
            else if (node == previousNode)
            {
                previousNode = node.Previous;
            }
        }

        public LinkedListIterator<T> Clone()
        {
            return new LinkedListIterator<T>(this);
        }
    }

    //class LinkedListIterator<T>
    //{
    //    public enum InitialPosition
    //    {
    //        Start, End
    //    }

    //    private LinkedList<T> list;

    //    private LinkedListNode<T> previousNode;
    //    private LinkedListNode<T> currentNode;
    //    private LinkedListNode<T> nextNode;

    //    public LinkedListIterator(LinkedList<T> list, InitialPosition initialPosition = InitialPosition.Start)
    //    {
    //        this.list = list;
    //        switch (initialPosition)
    //        {
    //            case InitialPosition.Start:
    //                this.nextNode = list.First;
    //                break;
    //            case InitialPosition.End:
    //                this.previousNode = list.Last;
    //                break;
    //            default:
    //                throw new ArgumentException();
    //        }
    //    }

    //    //clone constructor
    //    protected LinkedListIterator(LinkedListIterator<T> iterator)
    //    {
    //        this.list = iterator.list;
    //        this.previousNode = iterator.previousNode;
    //        this.currentNode = iterator.currentNode;
    //        this.nextNode = iterator.nextNode;
    //    }

    //    public bool HasPrevious()
    //    {
    //        if (previousNode != null)
    //        {
    //            if (!(previousNode.Next == null && previousNode.Previous == null) || list.First == previousNode)
    //            {
    //                return true;
    //            }
    //            else
    //            {
    //                //the previous node has been removed from the list
    //                previousNode = currentNode.Previous;
    //            }
    //        }
    //        return previousNode != null;
    //    }

    //    public T Previous()
    //    {
    //        this.currentNode = this.previousNode;
    //        this.nextNode = this.currentNode.Next;
    //        this.previousNode = this.currentNode.Previous;
    //        return currentNode.Value;
    //    }

    //    public bool hasNext()
    //    {
    //        if (nextNode != null)
    //        {
    //            if (nextNode.Next != null || nextNode.Previous != null)
    //            {
    //                return true;
    //            }
    //            else
    //            {
    //                //the next node has been removed from the list
    //                nextNode = currentNode.Next;
    //            }
    //        }
    //        return this.nextNode != null;
    //    }

    //    public T next()
    //    {
    //        this.currentNode = this.nextNode;
    //        this.nextNode = this.currentNode.Next;
    //        this.previousNode = this.currentNode.Previous;
    //        return currentNode.Value;
    //    }

    //    public void Remove()
    //    {
    //        this.list.Remove(this.currentNode);
    //        this.currentNode = null;
    //    }

    //    public void Set(T value)
    //    {
    //        this.currentNode.Value = value;
    //    }

    //    public LinkedListIterator<T> Clone()
    //    {
    //        return new LinkedListIterator<T>(this);
    //    }
    //}
}
