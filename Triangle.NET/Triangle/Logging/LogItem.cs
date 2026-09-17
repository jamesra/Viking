// -----------------------------------------------------------------------
// <copyright file="SimpleLogItem.cs" company="">
// Triangle.NET code by Christian Woltering, http://triangle.codeplex.com/
// </copyright>
// -----------------------------------------------------------------------

namespace TriangleNet.Logging
{
    using System;

    /// <summary>
    /// Represents an item stored in the log.
    /// </summary>
    public class LogItem(LogLevel level, string message, string info) : ILogItem
    {
        readonly DateTime time = DateTime.Now;
        readonly LogLevel level = level;
        readonly string message = message;
        readonly string info = info;

        public DateTime Time => time;

        public LogLevel Level => level;

        public string Message => message;

        public string Info => info;

        public LogItem(LogLevel level, string message)
            : this(level, message, "")
        { }
    }
}
