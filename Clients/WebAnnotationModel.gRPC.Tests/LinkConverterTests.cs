using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins the LocationLink, StructureLink and PermittedStructureLink converters and updaters between the
    /// <c>Viking.AnnotationServiceTypes.gRPC.V1.Protos</c> messages (and the <c>I*Link</c> interfaces) and the client
    /// objects. The client keeps <c>Bidirectional</c>, the server interfaces keep <c>Directional == !Bidirectional</c>;
    /// each converter inverts exactly once. Runs without a server.
    /// </summary>
    [TestFixture]
    public class LinkConverterTests
    {
        /// <summary>A non-proto <see cref="IStructureLink"/>, so the converter's interface overload (not the concrete one) runs.</summary>
        sealed class FakeStructureLink : IStructureLink
        {
            public ulong SourceID { get; set; }
            public ulong TargetID { get; set; }
            public bool Directional { get; set; }

            StructureLinkKey IDataObjectWithKey<StructureLinkKey>.ID { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            IStructureLinkKey IDataObjectWithKey<IStructureLinkKey>.ID { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public bool Equals(IStructureLinkKey other) => throw new NotSupportedException();
            public bool Equals(IStructureLink other) => throw new NotSupportedException();
        }

        /// <summary>A non-proto <see cref="IPermittedStructureLink"/>, so the converter's interface overload runs.</summary>
        sealed class FakePermittedStructureLink : IPermittedStructureLink
        {
            public ulong SourceTypeID { get; set; }
            public ulong TargetTypeID { get; set; }
            public bool Directional { get; set; }

            PermittedStructureLinkKey IDataObjectWithKey<PermittedStructureLinkKey>.ID { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            IPermittedStructureLinkKey IDataObjectWithKey<IPermittedStructureLinkKey>.ID { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public bool Equals(IPermittedStructureLink other) => throw new NotSupportedException();
        }

        // ---- LocationLink ----

        [Test]
        public void LocationLink_ServerToClient_MapsSourceAndTargetToAAndB()
        {
            var obj = new LocationLinkServerToClientConverter().Convert(new LocationLink { SourceId = 3, TargetId = 9 });

            Assert.That(obj.A, Is.EqualTo(3));
            Assert.That(obj.B, Is.EqualTo(9));
        }

        [Test]
        public void LocationLink_ServerToClient_OrdersTheEndsSmallerFirst()
        {
            // LocationLinkObj stores A < B whatever the order sent.
            var obj = new LocationLinkServerToClientConverter().Convert(new LocationLink { SourceId = 9, TargetId = 3 });

            Assert.That(obj.A, Is.EqualTo(3));
            Assert.That(obj.B, Is.EqualTo(9));
        }

        [Test]
        public void LocationLink_ServerToClient_InterfaceOverloadOnProtoUsesTheProto()
        {
            var converter = new LocationLinkServerToClientConverter();
            ILocationLink proto = new LocationLink { SourceId = 4, TargetId = 11 };

            var obj = ((IObjectConverter<ILocationLink, LocationLinkObj>)converter).Convert(proto);

            Assert.That(obj.A, Is.EqualTo(4));
            Assert.That(obj.B, Is.EqualTo(11));
        }

        [Test]
        public void LocationLink_ServerToClient_InterfaceOverloadOnOtherImplementationUsesAAndB()
        {
            // A LocationLinkObj is itself an ILocationLink and not a proto, so this takes the (long)A/(long)B path.
            ILocationLink other = new LocationLinkObj(20, 7);

            var obj = new LocationLinkServerToClientConverter().Convert(other);

            Assert.That(obj.A, Is.EqualTo(7));
            Assert.That(obj.B, Is.EqualTo(20));
        }

        [Test]
        public void LocationLink_ClientToServer_MapsAAndBToSourceAndTarget()
        {
            var proto = new LocationLinkClientToServerConverter().Convert(new LocationLinkObj(12, 5));

            Assert.That(proto.SourceId, Is.EqualTo(5));
            Assert.That(proto.TargetId, Is.EqualTo(12));
        }

        [Test]
        public void LocationLink_ClientToServer_InterfaceOverloadReturnsTheProto()
        {
            var converter = new LocationLinkClientToServerConverter();

            ILocationLink link = ((IObjectConverter<LocationLinkObj, ILocationLink>)converter).Convert(new LocationLinkObj(2, 8));

            Assert.That(link, Is.InstanceOf<LocationLink>());
            Assert.That(link.A, Is.EqualTo(2UL));
            Assert.That(link.B, Is.EqualTo(8UL));
        }

        [Test]
        public void LocationLink_RoundTrip_ReturnsTheSameKey()
        {
            var sent = new LocationLinkObj(41, 17);

            var back = new LocationLinkServerToClientConverter().Convert(new LocationLinkClientToServerConverter().Convert(sent));

            Assert.That(back.ID, Is.EqualTo(sent.ID));
        }

        [Test]
        public async Task LocationLink_Updater_ReportsNoChange()
        {
            var obj = new LocationLinkObj(1, 2);

            bool changed = await new LocationLinkServerToClientUpdater().Update(obj, new LocationLink { SourceId = 1, TargetId = 2 });

            Assert.That(changed, Is.False);
            Assert.That(obj.A, Is.EqualTo(1));
            Assert.That(obj.B, Is.EqualTo(2));
        }

        [Test]
        public void AddStandardLocationLinkConverters_RegistersAllFourConverters()
        {
            var services = new ServiceCollection();
            services.AddStandardLocationLinkConverters();
            using var provider = services.BuildServiceProvider();

            Assert.That(provider.GetService(typeof(IObjectConverter<LocationLink, LocationLinkObj>)), Is.InstanceOf<LocationLinkServerToClientConverter>());
            Assert.That(provider.GetService(typeof(IObjectConverter<ILocationLink, LocationLinkObj>)), Is.InstanceOf<LocationLinkServerToClientConverter>());
            Assert.That(provider.GetService(typeof(IObjectConverter<LocationLinkObj, LocationLink>)), Is.InstanceOf<LocationLinkClientToServerConverter>());
            Assert.That(provider.GetService(typeof(IObjectConverter<LocationLinkObj, ILocationLink>)), Is.InstanceOf<LocationLinkClientToServerConverter>());
        }

        // ---- StructureLink ----

        [TestCase(true)]
        [TestCase(false)]
        public void StructureLink_ServerToClient_CarriesBidirectionalAndIds(bool bidirectional)
        {
            var obj = new StructureLinkServerToClientConverter().Convert(
                new StructureLink { SourceId = 30, TargetId = 10, Bidirectional = bidirectional });

            Assert.That(obj.SourceID, Is.EqualTo(30));
            Assert.That(obj.TargetID, Is.EqualTo(10));
            Assert.That(obj.Bidirectional, Is.EqualTo(bidirectional));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void StructureLink_ServerToClient_InterfaceOverloadOnProtoUsesBidirectionalDirectly(bool bidirectional)
        {
            var converter = new StructureLinkServerToClientConverter();
            IStructureLink proto = new StructureLink { SourceId = 5, TargetId = 6, Bidirectional = bidirectional };

            var obj = ((IObjectConverter<IStructureLink, StructureLinkObj>)converter).Convert(proto);

            Assert.That(obj.Bidirectional, Is.EqualTo(bidirectional));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void StructureLink_ServerToClient_InterfaceOverloadInvertsDirectional(bool directional, bool expectedBidirectional)
        {
            var other = new FakeStructureLink { SourceID = 8, TargetID = 3, Directional = directional };

            var obj = new StructureLinkServerToClientConverter().Convert((IStructureLink)other);

            Assert.That(obj.SourceID, Is.EqualTo(8));
            Assert.That(obj.TargetID, Is.EqualTo(3));
            Assert.That(obj.Bidirectional, Is.EqualTo(expectedBidirectional));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void StructureLink_ClientToServer_CarriesBidirectionalAndIds(bool bidirectional)
        {
            var proto = new StructureLinkClientToServerConverter().Convert(new StructureLinkObj(30, 10, bidirectional));

            Assert.That(proto.SourceId, Is.EqualTo(30));
            Assert.That(proto.TargetId, Is.EqualTo(10));
            Assert.That(proto.Bidirectional, Is.EqualTo(bidirectional));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void StructureLink_RoundTrip_ReturnsTheSameKey(bool bidirectional)
        {
            var sent = new StructureLinkObj(30, 10, bidirectional);

            var back = new StructureLinkServerToClientConverter().Convert(new StructureLinkClientToServerConverter().Convert(sent));

            Assert.That(back.ID, Is.EqualTo(sent.ID));
        }

        /// <summary>
        /// The proto's <c>IStructureLink.Directional</c> and <c>IStructureLinkKey.Directional</c> are
        /// <c>!Bidirectional</c>, agree with the setter, and a key built through the interface is the proto's own key.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void StructureLinkProto_DirectionalIsTheInverseOfBidirectional(bool bidirectional)
        {
            var concrete = new StructureLink { SourceId = 5, TargetId = 6, Bidirectional = bidirectional };
            IStructureLink proto = concrete;

            Assert.That(proto.Directional, Is.EqualTo(!bidirectional));
            Assert.That(((IStructureLinkKey)concrete).Directional, Is.EqualTo(!bidirectional));
            Assert.That(new StructureLinkKey(proto), Is.EqualTo(new StructureLinkKey(5, 6, bidirectional)));
            Assert.That(concrete.CompareTo(new StructureLinkKey(5, 6, bidirectional)), Is.Zero);

            proto.Directional = bidirectional;
            Assert.That(proto.Directional, Is.EqualTo(bidirectional));
            Assert.That(concrete.Bidirectional, Is.EqualTo(!bidirectional));
        }

        [Test]
        public async Task StructureLink_Updater_ReportsNoChangeAndLeavesTheObject()
        {
            var obj = new StructureLinkObj(1, 2, true);

            bool changed = await new StructureLinkServerToClientUpdater().Update(obj, new StructureLink { SourceId = 1, TargetId = 2, Bidirectional = false });

            Assert.That(changed, Is.False);
            Assert.That(obj.Bidirectional, Is.True);
        }

        // ---- PermittedStructureLink ----

        [TestCase(true)]
        [TestCase(false)]
        public void PermittedStructureLink_ServerToClient_CarriesBidirectionalAndTypeIds(bool bidirectional)
        {
            var obj = new PermittedStructureLinkServerToClientConverter().Convert(
                new PermittedStructureLink { SourceTypeId = 7, TargetTypeId = 2, Bidirectional = bidirectional });

            Assert.That(obj.Bidirectional, Is.EqualTo(bidirectional));
            // The Obj constructor orders the ends only when bidirectional.
            Assert.That(obj.SourceTypeID, Is.EqualTo(bidirectional ? 2 : 7));
            Assert.That(obj.TargetTypeID, Is.EqualTo(bidirectional ? 7 : 2));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PermittedStructureLink_ServerToClient_InterfaceOverloadOnProtoUsesBidirectionalDirectly(bool bidirectional)
        {
            var converter = new PermittedStructureLinkServerToClientConverter();
            IPermittedStructureLink proto = new PermittedStructureLink { SourceTypeId = 1, TargetTypeId = 2, Bidirectional = bidirectional };

            var obj = ((IObjectConverter<IPermittedStructureLink, PermittedStructureLinkObj>)converter).Convert(proto);

            Assert.That(obj.Bidirectional, Is.EqualTo(bidirectional));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void PermittedStructureLink_ServerToClient_InterfaceOverloadInvertsDirectional(bool directional, bool expectedBidirectional)
        {
            var other = new FakePermittedStructureLink { SourceTypeID = 4, TargetTypeID = 9, Directional = directional };

            var obj = new PermittedStructureLinkServerToClientConverter().Convert((IPermittedStructureLink)other);

            Assert.That(obj.SourceTypeID, Is.EqualTo(4));
            Assert.That(obj.TargetTypeID, Is.EqualTo(9));
            Assert.That(obj.Bidirectional, Is.EqualTo(expectedBidirectional));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PermittedStructureLink_ClientToServer_CarriesBidirectionalAndTypeIds(bool bidirectional)
        {
            var proto = new PermittedStructureLinkClientToServerConverter().Convert(new PermittedStructureLinkObj(7, 2, bidirectional));

            Assert.That(proto.Bidirectional, Is.EqualTo(bidirectional));
            Assert.That(proto.SourceTypeId, Is.EqualTo(bidirectional ? 2 : 7));
            Assert.That(proto.TargetTypeId, Is.EqualTo(bidirectional ? 7 : 2));
        }

        [TestCase(DBACTION.NONE)]
        [TestCase(DBACTION.INSERT)]
        [TestCase(DBACTION.UPDATE)]
        [TestCase(DBACTION.DELETE)]
        public void PermittedStructureLink_ClientToServer_CarriesDbActionFromTheObject(DBACTION action)
        {
            var src = new PermittedStructureLinkObj(1, 2, false);
            // A new Obj starts at INSERT; INSERT -> UPDATE is ignored by the setter, so go through NONE first.
            src.DBAction = DBACTION.NONE;
            src.DBAction = action;

            var proto = new PermittedStructureLinkClientToServerConverter().Convert(src);

            Assert.That(((IChangeAction)proto).DBAction, Is.EqualTo(action));
        }

        [Test]
        public void PermittedStructureLink_ClientToServer_NewObjectIsAnInsert()
        {
            var proto = new PermittedStructureLinkClientToServerConverter().Convert(new PermittedStructureLinkObj(1, 2, true));

            Assert.That(((IChangeAction)proto).DBAction, Is.EqualTo(DBACTION.INSERT));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PermittedStructureLink_RoundTrip_ReturnsTheSameKey(bool bidirectional)
        {
            var sent = new PermittedStructureLinkObj(7, 2, bidirectional);

            var back = new PermittedStructureLinkServerToClientConverter().Convert(new PermittedStructureLinkClientToServerConverter().Convert(sent));

            Assert.That(back.ID, Is.EqualTo(sent.ID));
        }

        /// <summary>
        /// The proto's <c>IPermittedStructureLink.Directional</c> is <c>!Bidirectional</c> and agrees with the setter,
        /// <c>ToPermittedStructureLink</c>, the key built through the interface, and <c>Equals</c> between two protos.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void PermittedStructureLinkProto_DirectionalIsTheInverseOfBidirectional(bool bidirectional)
        {
            var concrete = new PermittedStructureLink { SourceTypeId = 1, TargetTypeId = 2, Bidirectional = bidirectional };
            IPermittedStructureLink proto = concrete;

            Assert.That(proto.Directional, Is.EqualTo(!bidirectional));
            Assert.That(new PermittedStructureLinkKey(proto), Is.EqualTo(new PermittedStructureLinkKey(1, 2, bidirectional)));
            Assert.That(concrete.Equals((IPermittedStructureLink)new PermittedStructureLink { SourceTypeId = 1, TargetTypeId = 2, Bidirectional = bidirectional }), Is.True);
            Assert.That(concrete.Equals((IPermittedStructureLink)new PermittedStructureLink { SourceTypeId = 1, TargetTypeId = 2, Bidirectional = !bidirectional }), Is.False);
            Assert.That(proto.ToPermittedStructureLink().Bidirectional, Is.EqualTo(bidirectional));

            proto.Directional = bidirectional;
            Assert.That(proto.Directional, Is.EqualTo(bidirectional));
            Assert.That(concrete.Bidirectional, Is.EqualTo(!bidirectional));
        }

        [Test]
        public async Task PermittedStructureLink_Updater_ReportsNoChangeAndLeavesTheObject()
        {
            var obj = new PermittedStructureLinkObj(1, 2, true);

            bool changed = await new PermittedStructureLinkServerToClientUpdater().Update(
                obj, new PermittedStructureLink { SourceTypeId = 1, TargetTypeId = 2, Bidirectional = false });

            Assert.That(changed, Is.False);
            Assert.That(obj.Bidirectional, Is.True);
        }
    }
}
