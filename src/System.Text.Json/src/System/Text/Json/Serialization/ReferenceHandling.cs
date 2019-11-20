// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text.Json
{
    /// <summary>
    /// This enum defines the various ways the <see cref="JsonSerializer"/> can deal with references on Serialization.
    /// </summary>
    public enum ReferenceHandling
    {
        /// <summary>
        /// On Serialization: Throw a JsonException when MaxDepth is exceeded, this may occur by either a Reference Loop or by passing a very deep object. This option will not affect the performance of the serializer.
        /// On Deserialization: No effect.
        /// </summary>
        Default = 0,

        /// <summary>
        /// On Serialization:  Ignores (skips writing) the property/element where the reference loop is detected.
        /// On Deserialization: No effect
        /// </summary>
        Ignore = 1,

        /// <summary>
        /// On Serialization: When writing complex types, the serializer also writes them metadata ($id, $values and $ref) properties in order re-use them by writing a reference to the object or array.
        /// On Deserialization: Metadata will be expected (although is not mandatory) and the deserializer will try to understand it.
        /// </summary>
        Preserve = 2,
    }
}
