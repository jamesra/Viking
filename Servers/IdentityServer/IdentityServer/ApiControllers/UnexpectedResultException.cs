using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Viking.Identity.Controllers
{ 
    /// <summary>
    /// Thrown from my code to prompt an IActionResult that is not the expected result for the operation
    /// </summary>
    public class UnexpectedResultException : Exception
    {
        public readonly IActionResult Result;

        public UnexpectedResultException([NotNull] IActionResult result)
        {
            Result = result;
        }

        public UnexpectedResultException([NotNull] IActionResult result, string message) : base(message)
        {
            Result = result;
        }

        public UnexpectedResultException([NotNull] IActionResult result, string message, Exception innerException) : base(message, innerException)
        {
            Result = result;
        }

        protected UnexpectedResultException([NotNull] IActionResult result, SerializationInfo info, StreamingContext context) : base(info, context)
        {
            Result = result;
        }
    } 
}