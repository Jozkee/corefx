﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace System.Text.Json
{
    public static partial class JsonSerializer
    {
        // AggressiveInlining used although a large method it is only called from one location and is on a hot path.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool WriteObject(
            JsonSerializerOptions options,
            Utf8JsonWriter writer,
            ref WriteStack state)
        {
            // Write the start.
            if (!state.Current.StartObjectWritten)
            {
                // If true, we are writing a root object or a value that doesn't belong
                // to an object e.g. a dictionary value.
                if (state.Current.CurrentValue == null)
                {
                    state.Current.WriteObjectOrArrayStart(ClassType.Object, writer, options, writeNull: true);
                    return WriteEndObject(ref state, writer, options);
                }

                //Handle reference here
                //if first seen
                //just write the property $id for objects;
                //or write { "$id": "#", "$values": current array } for arrays.
                //if seen before
                //just write { "$ref": "#" } and finish processing the object/array.

                ResolvedReferenceHandling handling = options.HandleReference(ref state, out string referenceId, out bool writeAsReference, state.Current.CurrentValue, threshold: options.EffectiveMaxDepth, currentDepth: writer.CurrentDepth);

                if (handling == ResolvedReferenceHandling.Ignore)
                {
                    //Reference loop found, do not write anything and pop the frame from the stack.
                    return WriteEndObject(ref state, writer, options);
                }

                //state.Current.WriteObjectOrArrayStart(ClassType.Object, writer, options);
                options.WriteStart(ref state.Current, ClassType.Object, writer, options, writeAsReference: writeAsReference, referenceId: referenceId);

                if (handling == ResolvedReferenceHandling.IsReference)
                {
                    return WriteEndObject(ref state, writer, options);
                }

                state.Current.MoveToNextProperty = true;
            }

            if (state.Current.MoveToNextProperty)
            {
                state.Current.NextProperty();
            }

            // Determine if we are done enumerating properties.
            if (state.Current.ExtensionDataStatus != ExtensionDataWriteStatus.Finished)
            {
                // If ClassType.Unknown at this point, we are typeof(object) which should not have any properties.
                Debug.Assert(state.Current.JsonClassInfo.ClassType != ClassType.Unknown);

                JsonPropertyInfo jsonPropertyInfo = state.Current.JsonClassInfo.PropertyCacheArray[state.Current.PropertyEnumeratorIndex - 1];
                HandleObject(jsonPropertyInfo, options, writer, ref state);

                return false;
            }

            writer.WriteEndObject();
            return WriteEndObject(ref state, writer, options);
        }

        private static bool WriteEndObject(ref WriteStack state, Utf8JsonWriter writer, JsonSerializerOptions options)
        {
            if (state.Current.PopStackOnEndObject)
            {
                state.Pop(writer, options);
            }

            return true;
        }

        // AggressiveInlining used although a large method it is only called from one location and is on a hot path.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void HandleObject(
            JsonPropertyInfo jsonPropertyInfo,
            JsonSerializerOptions options,
            Utf8JsonWriter writer,
            ref WriteStack state)
        {
            Debug.Assert(
                state.Current.JsonClassInfo.ClassType == ClassType.Object ||
                state.Current.JsonClassInfo.ClassType == ClassType.Unknown);

            if (!jsonPropertyInfo.ShouldSerialize)
            {
                state.Current.MoveToNextProperty = true;
                return;
            }

            bool obtainedValue = false;
            object currentValue = null;

            // Check for polymorphism.
            if (jsonPropertyInfo.ClassType == ClassType.Unknown)
            {
                currentValue = jsonPropertyInfo.GetValueAsObject(state.Current.CurrentValue);
                obtainedValue = true;
                GetRuntimePropertyInfo(currentValue, state.Current.JsonClassInfo, ref jsonPropertyInfo, options);
            }

            state.Current.JsonPropertyInfo = jsonPropertyInfo;

            if (jsonPropertyInfo.ClassType == ClassType.Value)
            {
                jsonPropertyInfo.Write(ref state, writer);
                state.Current.MoveToNextProperty = true;
                return;
            }

            // A property that returns an enumerator keeps the same stack frame.
            if (jsonPropertyInfo.ClassType == ClassType.Enumerable)
            {
                bool endOfEnumerable = HandleEnumerable(jsonPropertyInfo.ElementClassInfo, options, writer, ref state);
                if (endOfEnumerable)
                {
                    state.Current.MoveToNextProperty = true;
                }

                return;
            }

            // A property that returns a dictionary keeps the same stack frame.
            if (jsonPropertyInfo.ClassType == ClassType.Dictionary)
            {
                bool endOfEnumerable = HandleDictionary(jsonPropertyInfo.ElementClassInfo, options, writer, ref state);
                if (endOfEnumerable)
                {
                    state.Current.MoveToNextProperty = true;
                }

                return;
            }

            // A property that returns an object.
            if (!obtainedValue)
            {
                currentValue = jsonPropertyInfo.GetValueAsObject(state.Current.CurrentValue);
            }

            if (currentValue != null)
            {
                // A new stack frame is required.
                JsonPropertyInfo previousPropertyInfo = state.Current.JsonPropertyInfo;
                state.Current.MoveToNextProperty = true;

                JsonClassInfo nextClassInfo = jsonPropertyInfo.RuntimeClassInfo;
                state.Push(nextClassInfo, currentValue);

                // Set the PropertyInfo so we can obtain the property name in order to write it.
                state.Current.JsonPropertyInfo = previousPropertyInfo;
            }
            else
            {
                if (!jsonPropertyInfo.IgnoreNullValues)
                {
                    writer.WriteNull(jsonPropertyInfo.EscapedName.Value);
                }

                state.Current.MoveToNextProperty = true;
            }
        }
    }
}
