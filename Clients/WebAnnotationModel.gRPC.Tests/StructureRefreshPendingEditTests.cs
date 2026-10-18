using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins how <see cref="StructureServerToClientUpdater"/> treats a cached structure that has unsaved local edits
    /// (decision D-StructureRefreshPendingEdit): fields the user edited since the last save keep the user's value,
    /// every other field takes the server's value, and the queued DBAction is left as it was. Values the updater
    /// applies are never recorded as local edits, and a save (DBAction back to NONE) forgets the edits.
    /// Runs without a server: the updater is called directly on a hand-built structure.
    /// </summary>
    [TestFixture]
    public class StructureRefreshPendingEditTests
    {
        private const long Id = 30;
        private const long TypeId = 1;
        private const long CachedParentId = 10;
        private const long ServerParentId = 20;

        private readonly StructureServerToClientUpdater _updater = new StructureServerToClientUpdater();

        /// <summary>A structure as the cache holds it after a load: the cached values, DBAction NONE.</summary>
        private static StructureObj CachedStructure() =>
            new StructureObj(Id, TypeId)
            {
                Label = "cached label",
                Notes = "cached notes",
                Confidence = 0.25,
                ParentID = CachedParentId,
                DBAction = DBACTION.NONE,
            };

        private static Structure ServerStructure(string label = "server label", string notes = "server notes") =>
            new Structure
            {
                Id = Id,
                TypeId = TypeId,
                Label = label,
                Notes = notes,
                Confidence = 0.75,
                ParentId = ServerParentId,
                Username = "server user",
                Created = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                LastModified = Timestamp.FromDateTime(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            };

        [Test]
        public async Task LocallyEditedLabel_Refresh_KeepsLabelAndAppliesTheRest()
        {
            var obj = CachedStructure();
            obj.Label = "user label";

            await _updater.Update(obj, ServerStructure());

            Assert.That(obj.Label, Is.EqualTo("user label"));
            Assert.That(obj.Notes, Is.EqualTo("server notes"));
            Assert.That(obj.Confidence, Is.EqualTo(0.75));
            Assert.That(obj.ParentID, Is.EqualTo(ServerParentId));
            Assert.That(obj.Username, Is.EqualTo("server user"));
            Assert.That(obj.LastModified, Is.EqualTo(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.UPDATE), "the user's edit is still queued for Save");
        }

        [TestCase(nameof(StructureObj.Label))]
        [TestCase(nameof(StructureObj.Notes))]
        [TestCase(nameof(StructureObj.Confidence))]
        [TestCase(nameof(StructureObj.ParentID))]
        public async Task EachEditableField_EditedLocally_SurvivesRefresh(string field)
        {
            var obj = CachedStructure();
            switch (field)
            {
                case nameof(StructureObj.Label): obj.Label = "user label"; break;
                case nameof(StructureObj.Notes): obj.Notes = "user notes"; break;
                case nameof(StructureObj.Confidence): obj.Confidence = 0.5; break;
                case nameof(StructureObj.ParentID): obj.ParentID = null; break;
            }

            await _updater.Update(obj, ServerStructure());

            Assert.That(obj.Label, Is.EqualTo(field == nameof(StructureObj.Label) ? "user label" : "server label"));
            Assert.That(obj.Notes, Is.EqualTo(field == nameof(StructureObj.Notes) ? "user notes" : "server notes"));
            Assert.That(obj.Confidence, Is.EqualTo(field == nameof(StructureObj.Confidence) ? 0.5 : 0.75));
            Assert.That(obj.ParentID, Is.EqualTo(field == nameof(StructureObj.ParentID) ? (long?)null : ServerParentId));
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.UPDATE));
        }

        [Test]
        public async Task EditThenSave_Refresh_OverwritesEveryField()
        {
            var obj = CachedStructure();
            obj.Label = "user label";
            obj.DBAction = DBACTION.NONE; // what the store does once Save succeeds

            await _updater.Update(obj, ServerStructure());

            Assert.That(obj.Label, Is.EqualTo("server label"));
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.NONE));
        }

        [Test]
        public async Task EditThenSaveThenResubmit_Refresh_DoesNotKeepEditFromBeforeSave()
        {
            var obj = CachedStructure();
            obj.Label = "user label";
            obj.DBAction = DBACTION.NONE;
            obj.SubmitOnNextUpdate();

            await _updater.Update(obj, ServerStructure());

            Assert.That(obj.Label, Is.EqualTo("server label"), "the edit was saved; it is no longer pending");
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.UPDATE));
        }

        [Test]
        public async Task PendingEdit_RefreshValuesAreNotRecordedAsEdits()
        {
            var obj = CachedStructure();
            obj.Notes = "user notes";

            await _updater.Update(obj, ServerStructure(label: "first"));
            await _updater.Update(obj, ServerStructure(label: "second"));

            Assert.That(obj.Label, Is.EqualTo("second"), "a label the updater applied is not a local edit");
            Assert.That(obj.Notes, Is.EqualTo("user notes"));
        }

        [Test]
        public async Task UneditedStructure_Refresh_AppliesAllAndStaysUnqueued()
        {
            var obj = CachedStructure();

            bool changed = await _updater.Update(obj, ServerStructure());

            Assert.That(changed, Is.True);
            Assert.That(obj.Label, Is.EqualTo("server label"));
            Assert.That(obj.Notes, Is.EqualTo("server notes"));
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.NONE));
        }

        [Test]
        public async Task EditAfterRefresh_OnSameThread_IsRecordedAndQueued()
        {
            var obj = CachedStructure();
            await _updater.Update(obj, ServerStructure());

            obj.Label = "user label";
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.UPDATE), "server-apply scope ended with the refresh");

            await _updater.Update(obj, ServerStructure());
            Assert.That(obj.Label, Is.EqualTo("user label"));
        }

        [Test]
        public void VerifiedEdit_QueuesForSave()
        {
            var obj = CachedStructure();

            obj.Verified = true;

            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.UPDATE));
        }

        [Test]
        public async Task PendingDelete_Refresh_StaysQueuedForDelete()
        {
            var obj = CachedStructure();
            obj.DBAction = DBACTION.DELETE;

            await _updater.Update(obj, ServerStructure());

            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.DELETE), "a refresh must not turn a queued delete into an update");
        }

        /// <summary>
        /// A user edit made on another thread while the updater is applying server values must stay queued and keep
        /// its value; the updater must not reset DBAction to NONE over it.
        /// </summary>
        [Test]
        public async Task UserEditOnAnotherThreadDuringRefresh_IsKeptAndQueued()
        {
            var obj = CachedStructure();
            bool edited = false;
            obj.PropertyChanged += (s, e) =>
            {
                if (edited || e.PropertyName != nameof(StructureObj.Confidence))
                    return;
                edited = true;
                Task.Run(() => obj.Label = "user label").Wait();
            };

            await _updater.Update(obj, ServerStructure());

            Assert.That(edited, Is.True, "the updater applied Confidence before Label");
            Assert.That(obj.Label, Is.EqualTo("user label"));
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.UPDATE));
        }
    }
}
