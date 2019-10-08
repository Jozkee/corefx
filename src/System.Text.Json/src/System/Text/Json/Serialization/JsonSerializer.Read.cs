﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Json
{
    /// <summary>
    /// Provides functionality to serialize objects or value types to JSON and
    /// deserialize JSON into objects or value types.
    /// </summary>
    public static partial class JsonSerializer
    {
        private static void ReadCore(
            JsonSerializerOptions options,
            ref Utf8JsonReader reader,
            ref ReadStack readStack)
        {
            try
            {
                JsonReaderState initialState = default;
                long initialBytesConsumed = default;

                while (true)
                {
                    if (readStack.ReadAhead)
                    {
                        // When we're reading ahead we always have to save the state
                        // as we don't know if the next token is an opening object or
                        // array brace.
                        initialState = reader.CurrentState;
                        initialBytesConsumed = reader.BytesConsumed;
                    }

                    if (!reader.Read())
                    {
                        // Need more data
                        break;
                    }
                    JsonTokenType tokenType = reader.TokenType;

                    //Isolate logic for Reference Handling.
                    if (options.ReadReferenceHandling == ReferenceHandlingOnDeserialize.PreserveDuplicates && tokenType == JsonTokenType.PropertyName)
                    {
                        HandleMetadataProperty(options, ref reader, ref readStack);
                    }

                    //If no more metadata present, initialize the pending objects.
                    if ((tokenType == JsonTokenType.PropertyName && readStack.Current.LastMetaProperty == MetadataPropertyName.Unknown) || tokenType == JsonTokenType.StartArray || tokenType == JsonTokenType.EndObject)
                    {
                        InitEnqueued(options, ref readStack, ref reader);
                    }

                    if (JsonHelpers.IsInRangeInclusive(tokenType, JsonTokenType.String, JsonTokenType.False))
                    {
                        Debug.Assert(tokenType == JsonTokenType.String || tokenType == JsonTokenType.Number || tokenType == JsonTokenType.True || tokenType == JsonTokenType.False);

                        if (!HandleMetadataValue(tokenType, ref reader, ref readStack))
                        {
                            HandleValue(tokenType, options, ref reader, ref readStack);
                        }
                    }
                    else if (tokenType == JsonTokenType.PropertyName)
                    {
                        if (readStack.Current.LastMetaProperty == MetadataPropertyName.Unknown) //issue here, need to not get in here when doing $ref.
                        {
                            HandlePropertyName(options, ref reader, ref readStack);
                        }
                    }
                    else if (tokenType == JsonTokenType.StartObject)
                    {
                        if (readStack.Current.SkipProperty)
                        {
                            readStack.Push();
                            readStack.Current.Drain = true;
                        }
                        else if (readStack.Current.IsProcessingValue())
                        {
                            if (!HandleObjectAsValue(tokenType, options, ref reader, ref readStack, ref initialState, initialBytesConsumed))
                            {
                                // Need more data
                                break;
                            }
                        }
                        else if (readStack.Current.IsProcessingDictionary || readStack.Current.IsProcessingIDictionaryConstructible)
                        {
                            readStack.EnqueueInitTask(InitTaskType.Dictionary);
                            //HandleStartDictionary(options, ref reader, ref readStack);
                        }
                        else
                        {
                            readStack.EnqueueInitTask(InitTaskType.Object);
                            //HandleStartObject(options, ref readStack);
                        }
                    }
                    else if (tokenType == JsonTokenType.EndObject)
                    {
                        if (readStack.Current.LastMetaProperty == MetadataPropertyName.Ref)
                        {
                            object refValue = readStack.GetReference(readStack.Current.MetadataId);
                            readStack.Pop(); //an object reference has its own stack frame.

                            if (refValue == null)
                            {
                                HandleNull(ref reader, ref readStack);
                            }
                            else
                            {
                                // Reference to the entire Enumerable/Dictionary.
                                if (!readStack.Current.CollectionPropertyInitialized && (readStack.Current.IsEnumerableProperty || readStack.Current.IsDictionaryProperty))
                                {
                                    ApplyObjectToEnumerable(refValue, ref readStack, ref reader, setPropertyDirectly: true);
                                    readStack.Current.EndProperty();
                                }
                                else //Reference to an object within the enumerable/dictionary.
                                {
                                    ApplyObjectToEnumerable(refValue, ref readStack, ref reader);
                                }
                            }
                        }
                        else if (readStack.Current.HandleWrappingBrace)
                        {
                            // Skip wrapping brace of reference-preserved array.
                            //readStack.Current.RefValuesMetaProperty = MetadataPropertyName.Unknown;
                            readStack.Current.HandleWrappingBrace = false;
                            continue;
                        }
                        else if (readStack.Current.Drain)
                        {
                            readStack.Pop();

                            // Clear the current property in case it is a dictionary, since dictionaries must have EndProperty() called when completed.
                            // A non-dictionary property can also have EndProperty() called when completed, although it is redundant.
                            readStack.Current.EndProperty();
                        }
                        else if (readStack.Current.IsProcessingDictionary || readStack.Current.IsProcessingIDictionaryConstructible)
                        {
                            HandleEndDictionary(options, ref reader, ref readStack);
                        }
                        else
                        {
                            HandleEndObject(ref reader, ref readStack);
                        }
                    }
                    else if (tokenType == JsonTokenType.StartArray)
                    {
                        if (!readStack.Current.IsProcessingValue())
                        {
                            HandleStartArray(options, ref reader, ref readStack);
                        }
                        else if (!HandleObjectAsValue(tokenType, options, ref reader, ref readStack, ref initialState, initialBytesConsumed))
                        {
                            // Need more data
                            break;
                        }
                    }
                    else if (tokenType == JsonTokenType.EndArray)
                    {
                        bool HandleWrappingBrace = readStack.Current.EnumerableMetadataId != null;
                        readStack.Current.EnumerableMetadataId = null;
                        HandleEndArray(options, ref reader, ref readStack);
                        readStack.Current.HandleWrappingBrace = HandleWrappingBrace;
                    }
                    else if (tokenType == JsonTokenType.Null)
                    {
                        HandleNull(ref reader, ref readStack);
                    }
                }
            }
            catch (JsonReaderException ex)
            {
                // Re-throw with Path information.
                ThrowHelper.ReThrowWithPath(readStack, ex);
            }
            catch (FormatException ex) when (ex.Source == ThrowHelper.ExceptionSourceValueToRethrowAsJsonException)
            {
                ThrowHelper.ReThrowWithPath(readStack, reader, ex);
            }
            catch (InvalidOperationException ex) when (ex.Source == ThrowHelper.ExceptionSourceValueToRethrowAsJsonException)
            {
                ThrowHelper.ReThrowWithPath(readStack, reader, ex);
            }
            catch (JsonException ex)
            {
                ThrowHelper.AddExceptionInformation(readStack, reader, ex);
                throw;
            }

            readStack.BytesConsumed += reader.BytesConsumed;
            return;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HandleObjectAsValue(
            JsonTokenType tokenType,
            JsonSerializerOptions options,
            ref Utf8JsonReader reader,
            ref ReadStack readStack,
            ref JsonReaderState initialState,
            long initialBytesConsumed)
        {
            if (readStack.ReadAhead)
            {
                // Attempt to skip to make sure we have all the data we need.
                bool complete = reader.TrySkip();

                // We need to restore the state in all cases as we need to be positioned back before
                // the current token to either attempt to skip again or to actually read the value in
                // HandleValue below.

                reader = new Utf8JsonReader(
                    reader.OriginalSpan.Slice(checked((int)initialBytesConsumed)),
                    isFinalBlock: reader.IsFinalBlock,
                    state: initialState);
                Debug.Assert(reader.BytesConsumed == 0);
                readStack.BytesConsumed += initialBytesConsumed;

                if (!complete)
                {
                    // Couldn't read to the end of the object, exit out to get more data in the buffer.
                    return false;
                }

                // Success, requeue the reader to the token for HandleValue.
                reader.Read();
                Debug.Assert(tokenType == reader.TokenType);
            }

            HandleValue(tokenType, options, ref reader, ref readStack);
            return true;
        }

        private static ReadOnlySpan<byte> GetUnescapedString(ReadOnlySpan<byte> utf8Source, int idx)
        {
            // The escaped name is always longer than the unescaped, so it is safe to use escaped name for the buffer length.
            int length = utf8Source.Length;
            byte[] pooledName = null;

            Span<byte> unescapedName = length <= JsonConstants.StackallocThreshold ?
                stackalloc byte[length] :
                (pooledName = ArrayPool<byte>.Shared.Rent(length));

            JsonReaderHelper.Unescape(utf8Source, unescapedName, idx, out int written);
            ReadOnlySpan<byte> propertyName = unescapedName.Slice(0, written).ToArray();

            if (pooledName != null)
            {
                // We clear the array because it is "user data" (although a property name).
                new Span<byte>(pooledName, 0, written).Clear();
                ArrayPool<byte>.Shared.Return(pooledName);
            }

            return propertyName;
        }

        private static void InitEnqueued(JsonSerializerOptions options, ref ReadStack state, ref Utf8JsonReader reader)
        {
            while (state.PendingTasksCount > 0)
            {
                InitTask task = state.DequeueInitTask();
                if (task.Type == InitTaskType.Dictionary)
                {
                    HandleStartDictionary(options, ref reader, ref state);

                    if (task.MetadataId != null)
                    {
                        if (state.Current.IsProcessingIDictionaryConstructible)
                        {
                            throw new JsonException("Cannot preserve references for types that are immutable.");
                        }

                        object value = state.Current.IsDictionaryProperty ?
                            state.Current.JsonPropertyInfo.GetValueAsObject(state.Current.ReturnValue) :
                            state.Current.ReturnValue;

                        state.SetReference(task.MetadataId, value);
                    }
                }
                else if (task.Type == InitTaskType.Object)
                {
                    HandleStartObject(options, ref state);

                    //state.Current.MetadataId = task.MetadataId;
                    if (task.MetadataId != null)
                    {
                        state.SetReference(task.MetadataId, state.Current.ReturnValue);
                    }
                }

                //state.Current.LastMetaProperty = task.LastMetaProperty;
            }
        }
    }
}
