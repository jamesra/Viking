﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using WebAnnotationModel;


namespace WebAnnotationModelTest
{

    class EventLogger
    {
        public ConcurrentQueue<EventArgs> listAllEvents = new ConcurrentQueue<EventArgs>();
        public ConcurrentQueue<NotifyCollectionChangedEventArgs> listCollectionEvents = new ConcurrentQueue<NotifyCollectionChangedEventArgs>();
        public ConcurrentQueue<PropertyChangingEventArgs> listPropertyChangingEvents = new ConcurrentQueue<PropertyChangingEventArgs>();
        public ConcurrentQueue<PropertyChangedEventArgs> listPropertyChangedEvents = new ConcurrentQueue<PropertyChangedEventArgs>();

        /// <summary>
        /// Ensure we do not subscribe to the same object twice since this could mask a failure
        /// </summary>
        private readonly List<object> SubscribedCollectionChangedObjects = new List<object>();
        private readonly List<object> SubscribedPropertyChangedObjects = new List<object>();
        private readonly List<object> SubscribedPropertyChangingObjects = new List<object>();

        public void SubscribeToCollectionChangedEvents(INotifyCollectionChanged obj)
        {
            Assert.IsFalse(SubscribedCollectionChangedObjects.Contains(obj));
            obj.CollectionChanged += OnCollectionChanged;
            SubscribedCollectionChangedObjects.Add(obj);
        }

        public void SubscribeToPropertyChangedEvents(INotifyPropertyChanged obj)
        {
            Assert.IsFalse(SubscribedPropertyChangedObjects.Contains(obj));
            obj.PropertyChanged += OnPropertyChanged;
            SubscribedPropertyChangedObjects.Add(obj);
        }

        public void SubscribeToPropertyChangingEvents(INotifyPropertyChanging obj)
        {
            Assert.IsFalse(SubscribedPropertyChangingObjects.Contains(obj));
            obj.PropertyChanging += OnPropertyChanging;
            SubscribedPropertyChangingObjects.Add(obj);
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            listAllEvents.Enqueue(e);
            listCollectionEvents.Enqueue(e);
        }

        private void OnPropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            listAllEvents.Enqueue(e);
            listPropertyChangingEvents.Enqueue(e);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            listAllEvents.Enqueue(e);
            listPropertyChangedEvents.Enqueue(e);
        }

        public void PopObjectAddedEvent(object expected_obj)
        {
            Assert.IsTrue(listCollectionEvents.TryDequeue(out NotifyCollectionChangedEventArgs e));
            Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Add);
            Assert.AreEqual(expected_obj, e.NewItems[0]);
        }

        public void PopObjectReplacedEvent(object old_obj, object new_obj)
        {
            Assert.IsTrue(listCollectionEvents.TryDequeue(out NotifyCollectionChangedEventArgs e));
            Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Replace);
            Assert.AreEqual(e.OldItems[0], old_obj);
            Assert.AreEqual(e.NewItems[0], new_obj);
        }

        public void PopObjectRemovedEvent(object deleted_obj)
        {
            Assert.IsTrue(listCollectionEvents.TryDequeue(out NotifyCollectionChangedEventArgs e));
            Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Remove);
            Assert.AreEqual(e.OldItems[0], deleted_obj);
        }

        public void PopObjectRemovedEvent(object[] deleted_objs)
        {
            Assert.IsTrue(listCollectionEvents.TryDequeue(out NotifyCollectionChangedEventArgs e));
            Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Remove);

            foreach (object deleted in e.OldItems)
            {
                Assert.IsTrue(deleted_objs.Contains(deleted));
            }

        }

        public void PopObjectPropertyChangingEvent(object obj, string property)
        {
            Assert.IsTrue(listPropertyChangingEvents.TryDequeue(out PropertyChangingEventArgs e));
            Assert.AreEqual(e.PropertyName, property);
        }

        public void PopObjectPropertyChangedEvent(object obj, string property)
        {
            Assert.IsTrue(listPropertyChangedEvents.TryDequeue(out PropertyChangedEventArgs e));
            Assert.AreEqual(e.PropertyName, property);
        }
    }
}