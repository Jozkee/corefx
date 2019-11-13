﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Diagnostics;
using System.Text.Json.Serialization.Converters;

namespace System.Text.Json
{
    public static partial class JsonSerializer
    {
        private static void HandleStartDictionary(JsonSerializerOptions options, ref ReadStack state)
        {
            Debug.Assert(!state.Current.IsProcessingEnumerable());

            JsonPropertyInfo jsonPropertyInfo = state.Current.JsonPropertyInfo;
            if (jsonPropertyInfo == null)
            {
                jsonPropertyInfo = state.Current.JsonClassInfo.CreateRootProperty(options);
            }

            Debug.Assert(jsonPropertyInfo != null);

            // A nested object or dictionary, so push new frame.
            if (state.Current.CollectionPropertyInitialized)
            {
                state.Push();
                state.Current.JsonClassInfo = jsonPropertyInfo.ElementClassInfo;
                state.Current.InitializeJsonPropertyInfo();

                JsonClassInfo classInfo = state.Current.JsonClassInfo;

                if (state.Current.IsProcessingDictionary())
                {
                    object dictValue = ReadStackFrame.CreateDictionaryValue(ref state);

                    // If value is not null, then we don't have a converter so apply the value.
                    if (dictValue != null)
                    {
                        state.Current.ReturnValue = dictValue;
                        state.Current.DetermineIfDictionaryCanBePopulated(state.Current.ReturnValue);
                    }

                    state.Current.CollectionPropertyInitialized = true;
                }
                else if (state.Current.IsProcessingObject(ClassType.Object))
                {
                    if (classInfo.CreateObject == null)
                    {
                        ThrowHelper.ThrowNotSupportedException_DeserializeCreateObjectDelegateIsNull(classInfo.Type);
                    }

                    state.Current.ReturnValue = classInfo.CreateObject();
                }
                else
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(classInfo.Type);
                }

                return;
            }

            state.Current.CollectionPropertyInitialized = true;

            object value = ReadStackFrame.CreateDictionaryValue(ref state);
            if (value != null)
            {
                state.Current.DetermineIfDictionaryCanBePopulated(value);

                if (value != null)
                {
                    if (state.Current.ReturnValue != null)
                    {
                        // Special case: If the dictionary is a reference and the reference does not exist, avoid setting the value directly to ReturnValue in order to not write it when JsonIgnoreNull is set.
                        // In other words; delay setting the value.
                        state.Current.DictionaryCandidate = value;
                        state.Current.JsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, value);
                    }
                    else
                    {
                        // A dictionary is being returned directly, or a nested dictionary.
                        state.Current.ReturnValue = value;
                    }
                }
            }
        }

        private static void HandleEndDictionary(JsonSerializerOptions options, ref ReadStack state)
        {
            Debug.Assert(!state.Current.SkipProperty);

            if (state.Current.IsProcessingProperty(ClassType.Dictionary))
            {
                if (state.Current.TempDictionaryValues != null)
                {
                    JsonDictionaryConverter converter = state.Current.JsonPropertyInfo.DictionaryConverter;
                    state.Current.JsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, converter.CreateFromDictionary(ref state, state.Current.TempDictionaryValues, options));
                    state.Current.EndProperty();
                }
                else
                {
                    SetDictionaryCandidateAsValue(ref state);
                    // Handle special case of DataExtensionProperty where we just added a dictionary element to the extension property.
                    // Since the JSON value is not a dictionary element (it's a normal property in JSON) a JsonTokenType.EndObject
                    // encountered here is from the outer object so forward to HandleEndObject().
                    if (state.Current.JsonClassInfo.DataExtensionProperty == state.Current.JsonPropertyInfo)
                    {
                        HandleEndObject(ref state);
                    }
                    else
                    {
                        // We added the items to the dictionary already.
                        state.Current.EndProperty();
                    }
                }
            }
            else
            {
                object value;
                if (state.Current.TempDictionaryValues != null)
                {
                    JsonDictionaryConverter converter = state.Current.JsonPropertyInfo.DictionaryConverter;
                    value = converter.CreateFromDictionary(ref state, state.Current.TempDictionaryValues, options);
                }
                else
                {
                    value = state.Current.ReturnValue;
                }

                if (state.IsLastFrame)
                {
                    // Set the return value directly since this will be returned to the user.
                    state.Current.Reset();
                    state.Current.ReturnValue = value;
                }
                else
                {
                    state.Pop();
                    ApplyObjectToEnumerable(value, ref state);
                }
            }
        }

        private static void SetDictionaryCandidateAsValue(ref ReadStack state)
        {
            if (state.Current.DictionaryCandidate != null)
            {
                state.Current.JsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, state.Current.DictionaryCandidate);
                state.Current.DictionaryCandidate = null;
            }
        }
    }
}
