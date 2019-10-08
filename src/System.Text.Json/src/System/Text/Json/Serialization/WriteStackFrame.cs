// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace System.Text.Json
{
    internal struct WriteStackFrame
    {
        // The object (POCO or IEnumerable) that is being populated.
        public object CurrentValue;
        public JsonClassInfo JsonClassInfo;

        // Support Dictionary keys.
        public string KeyName;

        // The current IEnumerable or IDictionary.
        public IEnumerator CollectionEnumerator;
        // Note all bools are kept together for packing:
        public bool PopStackOnEndCollection;
        public bool IsIDictionaryConstructible;
        public bool IsIDictionaryConstructibleProperty;

        // The current object.
        public bool PopStackOnEndObject;
        public bool StartObjectWritten;
        public bool MoveToNextProperty;
        //For preservation object wrapper.
        public bool WriteWrappingBraceOnEndCollection;
        public bool KeepReferenceInSet;

        // The current property.
        public bool PropertyEnumeratorActive;
        public int PropertyEnumeratorIndex;
        public ExtensionDataWriteStatus ExtensionDataStatus;
        public JsonPropertyInfo JsonPropertyInfo;

        public void Initialize(Type type, JsonSerializerOptions options)
        {
            JsonClassInfo = options.GetOrAddClass(type);
            if (JsonClassInfo.ClassType == ClassType.Value || JsonClassInfo.ClassType == ClassType.Enumerable || JsonClassInfo.ClassType == ClassType.Dictionary)
            {
                JsonPropertyInfo = JsonClassInfo.PolicyProperty;
            }
            else if (JsonClassInfo.ClassType == ClassType.IDictionaryConstructible)
            {
                JsonPropertyInfo = JsonClassInfo.PolicyProperty;
                IsIDictionaryConstructible = true;
            }
        }

        public void WriteObjectOrArrayStart(ClassType classType, Utf8JsonWriter writer, JsonSerializerOptions options, bool writeNull = false, bool writeReferenceObject = false, int? preservedRefId = null)
        {
            if (JsonPropertyInfo?.EscapedName.HasValue == true)
            {
                WriteObjectOrArrayStart(classType, JsonPropertyInfo.EscapedName.Value, writer, writeNull, writeReferenceObject, preservedRefId);
            }
            else if (KeyName != null)
            {
                JsonEncodedText propertyName = JsonEncodedText.Encode(KeyName, options.Encoder);
                WriteObjectOrArrayStart(classType, propertyName, writer, writeNull, writeReferenceObject, preservedRefId);
            }
            else
            {
                Debug.Assert(writeNull == false);

                // Write start without a property name.
                if (writeReferenceObject)
                {
                    writer.WriteStartObject();
                    writer.WriteString("$ref", preservedRefId.ToString());
                    writer.WriteEndObject();
                }
                else if (classType == ClassType.Object || classType == ClassType.Dictionary || classType == ClassType.IDictionaryConstructible)
                {
                    writer.WriteStartObject();
                    if (preservedRefId != null)
                    {
                        writer.WriteString("$id", preservedRefId.ToString());
                    }
                    StartObjectWritten = true;
                }
                else
                {
                    Debug.Assert(classType == ClassType.Enumerable);
                    if (preservedRefId != null) // wrap array into an object with $id and $values metadtaa properties
                    {
                        writer.WriteStartObject();
                        writer.WriteString("$id", preservedRefId.ToString()); //it can be WriteString.
                        writer.WritePropertyName("$values");
                        WriteWrappingBraceOnEndCollection = true;
                    }
                    writer.WriteStartArray();
                }
            }
        }

        private void WriteObjectOrArrayStart(ClassType classType, JsonEncodedText propertyName, Utf8JsonWriter writer, bool writeNull, bool writeReferenceObject, int? preservedRefId)
        {
            if (writeNull)
            {
                writer.WriteNull(propertyName);
            }
            else if (writeReferenceObject) //is a reference? write { "$ref": "1" } regardless of the type.
            {
                writer.WriteStartObject(propertyName);
                writer.WriteString("$ref", preservedRefId.ToString());
                writer.WriteEndObject();
            }
            else if (classType == ClassType.Object ||
                classType == ClassType.Dictionary ||
                classType == ClassType.IDictionaryConstructible)
            {
                writer.WriteStartObject(propertyName);
                StartObjectWritten = true;
                if (preservedRefId != null)
                {
                    writer.WriteString("$id", preservedRefId.ToString());
                }
            }
            else
            {
                Debug.Assert(classType == ClassType.Enumerable);
                if (preservedRefId != null) // new reference? wrap array into an object with $id and $values metadtaa properties
                {
                    writer.WriteStartObject(propertyName);
                    writer.WriteString("$id", preservedRefId.ToString()); //it can be WriteString.
                    writer.WritePropertyName("$values");
                    writer.WriteStartArray();
                    WriteWrappingBraceOnEndCollection = true;
                }
                else
                {
                    writer.WriteStartArray(propertyName);
                }
            }
        }

        public void Reset()
        {
            CurrentValue = null;
            EndObject();
        }

        public void EndObject()
        {
            CollectionEnumerator = null;
            ExtensionDataStatus = ExtensionDataWriteStatus.NotStarted;
            IsIDictionaryConstructible = false;
            JsonClassInfo = null;
            PropertyEnumeratorIndex = 0;
            PropertyEnumeratorActive = false;
            PopStackOnEndCollection = false;
            PopStackOnEndObject = false;
            StartObjectWritten = false;
            EndProperty();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndProperty()
        {
            IsIDictionaryConstructibleProperty = false;
            JsonPropertyInfo = null;
            KeyName = null;
            MoveToNextProperty = false;
        }

        public void EndDictionary()
        {
            CollectionEnumerator = null;
            PopStackOnEndCollection = false;
        }

        public void EndArray()
        {
            CollectionEnumerator = null;
            PopStackOnEndCollection = false;
        }

        // AggressiveInlining used although a large method it is only called from one location and is on a hot path.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NextProperty()
        {
            EndProperty();

            if (PropertyEnumeratorActive)
            {
                int len = JsonClassInfo.PropertyCacheArray.Length;
                if (PropertyEnumeratorIndex < len)
                {
                    if ((PropertyEnumeratorIndex == len - 1) && JsonClassInfo.DataExtensionProperty != null)
                    {
                        ExtensionDataStatus = ExtensionDataWriteStatus.Writing;
                    }

                    PropertyEnumeratorIndex++;
                    PropertyEnumeratorActive = true;
                }
                else
                {
                    PropertyEnumeratorActive = false;
                }
            }
            else
            {
                ExtensionDataStatus = ExtensionDataWriteStatus.Finished;
            }
        }
    }
}
