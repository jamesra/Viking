using System;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace WebAnnotationModel.gRPC
{
    public static class QueryLoggerExtension
    {
        public static IServiceCollection AddStandardQueryLogger(this IServiceCollection service)
        {
            service.AddSingleton<IQueryLogger>(
                new QueryLogger());
            service.AddSingleton<ISectionQueryLogger>(
                new QueryLogger()); 
            return service;
        }
    }

    public class QueryLogger : IQueryLogger, ISectionQueryLogger
    {
        public void LogQuery(string Description, long numObjects, DateTime StartTime, DateTime QueryEndTime, DateTime ParseEndTime)
        {
            Trace.WriteLine(PrepareQueryDetails(Description, numObjects, StartTime, QueryEndTime, ParseEndTime));
        }

        public void LogQuery(string Description, long numObjects, DateTime StartTime, DateTime QueryEndTime, DateTime ParseEndTime, DateTime EventsEndTime)
        {
            Trace.WriteLine(PrepareQueryDetails(Description, numObjects, StartTime, QueryEndTime, ParseEndTime, EventsEndTime));
        }

        public void LogQuery(string Description, long SectionNumber, long numObjects, DateTime StartTime, DateTime QueryEndTime, DateTime ParseEndTime)
        { 
            Trace.WriteLine(PrepareQueryDetails(Description, SectionNumber, numObjects, StartTime, QueryEndTime, ParseEndTime));
        }

        public void LogQuery(string Description, long SectionNumber, long numObjects, DateTime StartTime, DateTime QueryEndTime, DateTime ParseEndTime, DateTime EventsEndTime)
        { 
             Trace.WriteLine(PrepareQueryDetails(Description, SectionNumber, numObjects, StartTime, QueryEndTime, ParseEndTime, EventsEndTime));
        }
         
        private string PrepareQueryDetails(string Description, long SectionNumber, long numObjects, DateTime StartTime, DateTime QueryEndTime, DateTime ParseEndTime)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Sxn {0} finished {1} query, {2} returned\n", SectionNumber, Description, numObjects);
            sb.AppendFormat("\tQuery time: {0} (sec)\n", new TimeSpan(QueryEndTime.Ticks - StartTime.Ticks).TotalSeconds);
            sb.AppendFormat("\tParse time: {0} (sec)\n", new TimeSpan(ParseEndTime.Ticks - QueryEndTime.Ticks).TotalSeconds);

            return sb.ToString();
        }

        private string PrepareQueryDetails(string Description, long SectionNumber, long numObjects, DateTime StartTime, DateTime QueryEndTime, DateTime ParseEndTime, DateTime EventsEndTime)
        {
            StringBuilder sb = new StringBuilder(PrepareQueryDetails(Description, SectionNumber, numObjects, StartTime, QueryEndTime, ParseEndTime));
            sb.AppendFormat("\tEvents Time: {0} (sec)\n", new TimeSpan(EventsEndTime.Ticks - ParseEndTime.Ticks).TotalSeconds);
            return sb.ToString();
        }

        private string PrepareQueryDetails(string Description, long numObjects, DateTime StartTime, DateTime QueryEndTime, DateTime ParseEndTime)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Finished {1} query, {2} returned\n", Description, numObjects);
            sb.AppendFormat("\tQuery time: {0} (sec)\n", new TimeSpan(QueryEndTime.Ticks - StartTime.Ticks).TotalSeconds);
            sb.AppendFormat("\tParse time: {0} (sec)\n", new TimeSpan(ParseEndTime.Ticks - QueryEndTime.Ticks).TotalSeconds);

            return sb.ToString();
        }

        private string PrepareQueryDetails(string Description, long numObjects, DateTime StartTime, DateTime QueryEndTime, DateTime ParseEndTime, DateTime EventsEndTime)
        {
            StringBuilder sb = new StringBuilder(PrepareQueryDetails(Description, numObjects, StartTime, QueryEndTime, ParseEndTime));
            sb.AppendFormat("\tEvents Time: {0} (sec)\n", new TimeSpan(EventsEndTime.Ticks - ParseEndTime.Ticks).TotalSeconds);
            return sb.ToString();
        }
    }
}
