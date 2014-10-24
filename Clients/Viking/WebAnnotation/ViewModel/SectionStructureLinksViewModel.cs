/*
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Geometry;
using WebAnnotation.UI;
using Viking.ViewModels;
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{
    /*
    class SectionStructureLinksViewModel : System.Windows.IWeakEventListener
    {
        /// <summary>
        /// The section we store annotations for
        /// <summary>
        public readonly SectionViewModel Section;


        /// <summary>
        /// Allows us to describe all the StructureLinks visible on a screen
        /// </summary>
        private LineSearchGrid<StructureLink> StructureLinksSearch = null;


        /// <summary>
        /// This is a symptom of being halfway to the Jotunn architecture.  This is a pointer to the 
        /// parent section viewer control which can perform transforms
        /// </summary>
        public readonly Viking.UI.Controls.SectionViewerControl parent;

        public SectionStructureLinksViewModel(SectionViewModel section, Viking.UI.Controls.SectionViewerControl Parent)
        {
            this.parent = Parent;
            Trace.WriteLine("Create SectionLocationsViewModel for " + section.Number.ToString());
            this.Section = section;

            GridRectangle bounds = AnnotationOverlay.SectionBounds(parent, parent.Section.Number);
             
            StructureLinksSearch = new LineSearchGrid<StructureLink>(bounds, 10000);

            CollectionChangedEventManager.AddListener(Store.Structures, this);
            CollectionChangedEventManager.AddListener(Store.StructureLinks, this);
            CollectionChangedEventManager.AddListener(Store.Locations, this);
        }

        
        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            System.Collections.Specialized.NotifyCollectionChangedEventArgs CollectionChangeArgs = e as System.Collections.Specialized.NotifyCollectionChangedEventArgs;
            if (CollectionChangeArgs != null)
            {
                Type senderType = sender.GetType();
                if (senderType == typeof(StructureStore))
                {
                    this.OnStructuresStoreChanged(sender, CollectionChangeArgs);
                    return true;
                }
                else if (senderType == typeof(StructureLinkStore))
                {
                    this.OnStructureLinksStoreChanged(sender, CollectionChangeArgs);
                    return true;
                }
                else if (senderType == typeof(LocationStore))
                {
                    this.OnLocationStoreChanged(sender, CollectionChangeArgs);
                    return true;
                }
            }

            PropertyChangedEventArgs PropertyChangedArgs = e as PropertyChangedEventArgs;
            if (PropertyChangedArgs != null)
            {
                if (sender.GetType() == typeof(LocationObj))
                {
                    OnLocationPropertyChanged(sender, PropertyChangedArgs);
                    return true;
                }
            }

            PropertyChangingEventArgs PropertyChangingArgs = e as PropertyChangingEventArgs;
            if (PropertyChangingArgs != null)
            {
                if (sender.GetType() == typeof(LocationObj))
                {
                    OnLocationPropertyChanging(sender, PropertyChangingArgs);
                    return true;
                }
            }

            Debug.Fail("Weak Event not handled");
            return false;
        }
    } 
} */
