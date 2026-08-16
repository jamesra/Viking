using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using VikingXNA; 
using VikingXNAGraphics; 
using WebAnnotation;

namespace WebAnnotationTests
{
    class DummyAnnotation : ICanvasView
    {
        public IShape2D Shape;
         

        public DummyAnnotation(IShape2D shape)
        {
            this.Shape = shape; 
        }

        public Rectangle BoundingBox => Shape.BoundingBox;

        public int VisualHeight => throw new NotImplementedException();

        public bool Contains(Vector2 Position)
        {
            return Shape.Covers(Position);
        }

        public double Distance(Vector2 Position)
        {
            return 0;
        }

        public double DistanceFromCenterNormalized(Vector2 Position)
        {
            return 0;
        }

        public bool Intersects(LineSegment line)
        {
            return Shape.Intersects(line);
        }

        public bool IsVisible(Scene scene)
        {
            return true; 
        }
    }

    class DummyContainerAnnotation : DummyAnnotation, ICanvasViewContainer
    {
        public DummyAnnotation[] Children;

        public DummyContainerAnnotation(IShape2D shape, IEnumerable<IShape2D> children) : base(shape)
        {
            this.Children = children.Select(c => new DummyAnnotation(c)).ToArray(); 
        }

        public ICanvasView GetAnnotationAtPosition(Vector2 position)
        {
            foreach(var dc in Children)
            {
                if (dc.Contains(position))
                    return dc;
            }

            if (this.Contains(position))
                return this;

            return null;
        }
    }


    class DummyCanvas : ICanvasViewHitTesting
    {
        List<DummyAnnotation> Annotations = new List<DummyAnnotation>();

        public void Add(DummyAnnotation a)
        {
            Annotations.Add(a);
        }

        public List<HitTestResult> GetAnnotations(Vector2 WorldPosition)
        {
            var results = new List<HitTestResult>();
            foreach(DummyAnnotation annotation in Annotations)
            {
                if(annotation.Contains(WorldPosition))
                {
                    var output = new HitTestResult(annotation, 0, 0, 0);
                    results.Add(output);
                }
            }

            return results;
        }

        public List<HitTestResult> GetAnnotations(LineSegment line)
        {
            var results = new List<HitTestResult>();
            foreach (DummyAnnotation annotation in Annotations)
            {
                if (annotation.Shape.Intersects(line))
                {
                    var output = new HitTestResult(annotation, 0, 0, 0);
                    results.Add(output);
                }
            }

            return results;
        }

        public List<HitTestResult> GetAnnotations(Rectangle rect)
        {
            var results = new List<HitTestResult>();
            foreach (DummyAnnotation annotation in Annotations)
            {
                if (annotation.Shape.Intersects(rect) )
                {
                    var output = new HitTestResult(annotation, 0, 0, 0);
                    results.Add(output);
                }
            }

            return results;
        }
    }
}
