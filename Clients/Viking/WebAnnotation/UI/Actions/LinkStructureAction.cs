using Microsoft.Xna.Framework;
using System;
using Viking.AnnotationServiceTypes;
using VikingXNAGraphics;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Actions
{
    class LinkStructureAction : IAction, IActionView, IEquatable<LinkStructureAction>
    {
        public readonly LocationObj Source;
        public readonly LocationObj Target;
        public readonly bool Bidirectional;

        public readonly StructureLinkKey Link;

        public LocationAction Type => LocationAction.CREATELINK;

        public Action Execute => OnExecute;

        public static implicit operator Action(LinkStructureAction a) => a.Execute;

        public IRenderable Passive { get; set; } = null;

        public IRenderable Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.Connect;

        public LinkStructureAction(LocationObj source, LocationObj target, bool Bidirectional)
        {
            this.Source = source;
            this.Target = target;
            this.Bidirectional = Bidirectional;

            Link = new StructureLinkKey(source.ParentID.Value, target.ParentID.Value, Bidirectional);

            CreateDefaultVisuals();
        }

        public void OnExecute()
        {
            StructureLinkObj linkStruct = new StructureLinkObj(Source.ParentID.Value, Target.ParentID.Value, Bidirectional);
            linkStruct = Store.StructureLinks.Create(linkStruct);
        }

        public void CreateDefaultVisuals()
        {
            LineView view = new LineView(Source.VolumePosition, Target.VolumePosition, Math.Min(Source.Radius, Target.Radius), Color.White.SetAlpha(0.5f), LineStyle.AnimatedLinear);
            Passive = view;
            Active = new LineView(Source.VolumePosition, Target.VolumePosition, Math.Min(Source.Radius, Target.Radius), Color.White.SetAlpha(1f), LineStyle.AnimatedLinear);
        }


        public bool Equals(IAction other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (this.Type != other.Type)
                return false;

            LinkStructureAction other_action = other as LinkStructureAction;
            if (other_action == null)
                return false;

            return this.Equals(other_action);
        }

        public bool Equals(LinkStructureAction other)
        {
            return other.Link == this.Link;
        }
    }
}
