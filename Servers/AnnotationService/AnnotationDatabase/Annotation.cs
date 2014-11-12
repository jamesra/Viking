using System;
using System.Linq; 
using System.Data.Linq; 
using System.Data.Linq.Mapping;
using System.Reflection;
using System.Collections.Generic; 

namespace Annotation.Database
{

    partial class AnnotationDataContext
    {
        private static List<T> ResultOrEmptyList<T>(IMultipleResults multipleResults)
            where T : class
        {
            IEnumerable<T> result = multipleResults.GetResult<T>();
            if (result == null)
            {
                return new List<T>();
            }

            return new List<T>(result);
        }

        /// <summary>
        /// Our server didn't exist before 2007 and if we pass a date earlier than 1753 the SQL query fails
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static DateTime? ValidateDate(DateTime? input)
        {
            if (input.HasValue == false)
                return input; 

            if (input < new DateTime(2007, 1, 1))
                return new DateTime(2007,1,1);
            else
                return input; 
        }
 
        public long[] SelectUnfinishedStructureBranches(long StructureID)
        {
            IMultipleResults results = InternalSelectUnfinishedStructureBranches(StructureID);
            IEnumerable<long> enumerator = results.GetResult<long>();
            if(enumerator == null)
            {
                return new long[0];
            }

            return new List<long>(enumerator).ToArray();
        }

        public List<DBStructure> SelectRootStructures()
        {
            IMultipleResults results = InternalSelectRootStructures();

            return ResultOrEmptyList<DBStructure>(results);
        }

        public IList<DBLocation> SectionLocationsAndLinks(double z)
        {
            IMultipleResults results = InternalSelectSectionLocationsAndLinks(new double?(z), new DateTime?());

            return CreateLocationsHeirarchy(results);
        }

        public IList<DBLocation> SectionLocationsAndLinks(double z, DateTime? ModifiedAfter)
        {
            IMultipleResults results = InternalSelectSectionLocationsAndLinks(new double?(z), ValidateDate(ModifiedAfter));

            return CreateLocationsHeirarchy(results);
        }

        class DBLocationComparer : IComparer<DBLocation>
        { 
            int IComparer<DBLocation>.Compare(DBLocation x, DBLocation y)
            {
                return (int)(x.ID - y.ID);
            }
        }

        class DBLocationLinkComparer : IComparer<DBLocationLink>
        {
            int IComparer<DBLocationLink>.Compare(DBLocationLink x, DBLocationLink y)
            {

                int diff = (int)(x.LinkedTo - y.LinkedTo);
                if(diff == 0)
                {
                    diff = (int)(x.LinkedFrom - y.LinkedFrom);
                }

                return diff; 
            }
        }
         
        /// <summary>
        /// This was the slowest portion of location queries.  Old solutions have been commented.  The slowest version 
        /// </summary>
        /// <param name="results"></param>
        /// <returns></returns>
        private IList<DBLocation> CreateLocationsHeirarchy(IMultipleResults results)
        {
            List<DBLocation> listLocations = ResultOrEmptyList<DBLocation>(results);
            List<DBLocationLink> listLocLinks = ResultOrEmptyList<DBLocationLink>(results);


            /* Do not do this, very slow runtime
            var linkedToGroups = listLocLinks.GroupBy(link => link.LinkedTo);
            var linkedFromGroups = listLocLinks.GroupBy(link => link.LinkedFrom);

            listLocations.ForEach(
                loc => loc.IsLinkedFrom.SetSource(
                    linkedToGroups.Where(link => link.Key == loc.ID).FirstOrDefault()));

            listLocations.ForEach(
                loc => loc.IsLinkedTo.SetSource(
                    linkedFromGroups.Where(link => link.Key == loc.ID).FirstOrDefault()));
            */

            
            // About 3 seconds for 6000 locations
            SortedList<long, DBLocation> sortedLocations = new SortedList<long, DBLocation>(listLocations.Count);
            listLocations.ForEach(loc => sortedLocations.Add(loc.ID, loc));
              
            var linkedToGroups = listLocLinks.GroupBy(link => link.LinkedTo);
            var linkedFromGroups = listLocLinks.GroupBy(link => link.LinkedFrom);

            foreach (var link in linkedToGroups)
            {
                if(sortedLocations.ContainsKey(link.Key))
                    sortedLocations[link.Key].IsLinkedFrom.SetSource(link);
            }

            foreach( var link in linkedFromGroups)
            {
                if (sortedLocations.ContainsKey(link.Key))
                    sortedLocations[link.Key].IsLinkedTo.SetSource(link);
            }
             
            return sortedLocations.Values;

            /* Reasonably fast, about eight seconds for 6000 locations
            listLocations.ForEach(
                loc => loc.IsLinkedFrom.SetSource(listLocLinks.Where(
                    link => link.LinkedTo == loc.ID)));

            listLocations.ForEach(
                loc => loc.IsLinkedTo.SetSource(listLocLinks.Where(
                    link => link.LinkedFrom == loc.ID)));
            */
            //return listLocations; 
        }

        [Function(Name = "SelectSectionLocationsAndLinks")]
        [ResultType(typeof(DBLocation))]
        [ResultType(typeof(DBLocationLink))]
        protected  IMultipleResults InternalSelectSectionLocationsAndLinks([global::System.Data.Linq.Mapping.ParameterAttribute(Name = "z", DbType = "float")] System.Nullable<double> section,
                                                                              [global::System.Data.Linq.Mapping.ParameterAttribute(Name = "QueryDate", DbType = "datetime")] System.Nullable<DateTime> ModifiedAfter)
        {
            IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)(MethodInfo.GetCurrentMethod()), section, ModifiedAfter);

            return (IMultipleResults)result.ReturnValue; 
        }

        public List<DBStructure> SelectAllStructuresAndLinks()
        {
            IMultipleResults results = InternalSelectStructuresAndLinks();

            return CreateStructureHeirarchy(results);
        }

        public List<DBStructure> SelectStructuresAndLinks(long SectionNumber, DateTime? ModifiedAfter)
        {
            IMultipleResults results = InternalSelectStructuresAndLinks(SectionNumber, ValidateDate(ModifiedAfter));

            return CreateStructureHeirarchy(results);
        }

        public List<DBStructure> CreateStructureHeirarchy(IMultipleResults results)
        {
            List<DBStructure> listStructs = ResultOrEmptyList<DBStructure>(results);
            List<DBStructureLink> listStructLinks = ResultOrEmptyList<DBStructureLink>(results);

            listStructs.ForEach(
                s => s.IsSourceOf.SetSource(listStructLinks.Where(
                    link => link.SourceID == s.ID)));

            listStructs.ForEach(
                s => s.IsTargetOf.SetSource(listStructLinks.Where(
                    link => link.TargetID == s.ID)));

            return listStructs;

        }

        [Function(Name = "SelectStructuresAndLinks")]
        [ResultType(typeof(DBStructure))]
        [ResultType(typeof(DBStructureLink))]
        protected IMultipleResults InternalSelectStructuresAndLinks()
        {
            IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)(MethodInfo.GetCurrentMethod()), new object[0]);

            return (IMultipleResults)result.ReturnValue;
        }

        [Function(Name = "SelectStructuresForSection")]
        [ResultType(typeof(DBStructure))]
        [ResultType(typeof(DBStructureLink))]
        protected IMultipleResults InternalSelectStructuresAndLinks([ParameterAttribute(Name = "z", DbType = "float")] System.Nullable<double> section,
                                                                    [ParameterAttribute(Name = "QueryDate", DbType = "datetime")] System.Nullable<DateTime> ModifiedAfter)
        {
            IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)(MethodInfo.GetCurrentMethod()), section, ModifiedAfter);

            return (IMultipleResults)result.ReturnValue;
        }

        [Function(Name = "SelectRootStructures")]
        [ResultType(typeof(DBStructure))]
        protected IMultipleResults InternalSelectRootStructures()
        {
            IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)(MethodInfo.GetCurrentMethod()));

            return (IMultipleResults)result.ReturnValue;
        }

        [Function(Name = "SelectUnfinishedStructureBranches")]
        [ResultType(typeof(long))]
        public IMultipleResults InternalSelectUnfinishedStructureBranches(long StructureID)
        {
            IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)(MethodInfo.GetCurrentMethod()), StructureID);
            return (IMultipleResults)result.ReturnValue;
        }

    }
}
