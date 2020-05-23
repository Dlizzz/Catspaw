using System;

namespace Catspaw.Pioneer
{
#pragma warning disable CA2237 // Ajoutez [Serializable] à AvrException, car ce type implémente ISerializable

    /// <summary>Exception class for the AVR</summary>
    // [Serializable]
    public class AvrException : Exception
    {
        /// <summary>Create a simple AVR exception</summary>
        public AvrException() { }
        /// <summary>Create an AVR exception with message</summary>
        /// <param name="message"></param>
        public AvrException(string message): base(message) { }
        /// <summary>Create an AVR exception with message and inner exception</summary>
        /// <param name="message"></param>
        /// <param name="inner"></param>
        public AvrException(string message, Exception inner): base(message, inner) { }
    }
#pragma warning restore CA2237 // Ajoutez [Serializable] à AvrException, car ce type implémente ISerializable
}
