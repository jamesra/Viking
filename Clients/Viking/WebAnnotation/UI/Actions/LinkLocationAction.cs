using Microsoft.Xna.Framework;
using System;
using Viking.AnnotationServiceTypes;
using VikingXNAGraphics;
using WebAnnotationModel;

namespace WebAnnotation.UI.Actions
{
    class LinkLocationAction : IAction, IActionView, IEquatable<LinkLocationAction>
    {
        public readonly LocationObj A;
        public readonly LocationObj B;

        public readonly LocationLinkKey Link;

        public LocationAction Type => LocationAction.CREATELINK;

        public Action Execute => OnExecute;

        public static implicit operator Action(LinkLocationAction a) => a.Execute;

        public IRenderable Passive { get; set; } = null;

        public IRenderable Active { get; set; } = null;

        public BuiltinTexture Icon { get; set; } = BuiltinTexture.Chain;

        public LinkLocationAction(LocationObj A, LocationObj B)
        {
            this.A = A;
            this.B = B;

            Link = new LocationLinkKey(A.ID, B.ID);

            CreateDefaultVisuals();
        }

        public LinkLocationAction(LocationLinkKey link)
        {
            this.A = Store.Locations[link.A];
            this.B = Store.Locations[link.B];

            Link = link;

            CreateDefaultVisuals();
        }

        public void OnExecute()
        {
            try
            {
                Store.LocationLinks.CreateLink(A.ID, B.ID);
                //This save was here when I added the exceptions... but it appears redundant looking at the code
                Store.LocationLinks.Save();
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
            }
        }

        public void CreateDefaultVisuals()
        {
            LineView view = new LineView(A.VolumePosition, B.VolumePosition, Math.Min(A.Radius, B.Radius), Color.White.SetAlpha(0.5f), LineStyle.Standard);
            Passive = view;
            Active = new LineView(A.VolumePosition, B.VolumePosition, Math.Min(A.Radius, B.Radius), Color.White.SetAlpha(1f), LineStyle.Standard); ;
        }

        public bool Equals(IAction other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (this.Type != other.Type)
                return false;

            LinkLocationAction other_action = other as LinkLocationAction;
            if (other_action == null)
                return false;

            return this.Equals(other_action);
        }

        public bool Equals(LinkLocationAction other)
        {
            return other.Link == this.Link;
        }
    }
}
