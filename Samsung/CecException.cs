using System;

namespace Catspaw.Samsung
{
#pragma warning disable CA2237 // Ajoutez [Serializable] à CecException, car ce type implémente ISerializable
    /// <summary>Exception class for Cec bus</summary>
    // [Serializable]
    public class CecException : Exception
    {
        /// <summary>Create a simple Cec exception</summary>
        public CecException() { }
        /// <summary>Create a Cec exception with message</summary>
        /// <param name="message"></param>
        public CecException(string message): base(message) { }
        /// <summary>Create a Cec exception with message and inner exception</summary>
        /// <param name="message"></param>
        /// <param name="inner"></param>
        public CecException(string message, Exception inner): base(message, inner) { }
    }
#pragma warning restore CA2237 // Ajoutez [Serializable] à CecException, car ce type implémente ISerializable
}
