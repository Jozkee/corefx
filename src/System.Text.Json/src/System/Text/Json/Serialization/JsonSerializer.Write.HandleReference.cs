using System.Reflection;
using System.Text.Json.Serialization;

namespace System.Text.Json
{
    public static partial class JsonSerializer
    {
        private enum ResolvedReferenceHandling
        {
            Ignore,
            Preserve,
            None //Ignore or Error were selected but no loop was found.
        }

        private static ResolvedReferenceHandling HandleReference(JsonSerializerOptions options, ref WriteStack state, object currentPropertyValue = null)
        {
            object currentValue = currentPropertyValue ?? state.Current.CurrentValue;

            //if jsonPropertyInfo == null
            //We are in the root object.

            ReferenceHandling handling;
            //JsonProperty is null if is either root object or and object element within an array.
            //First look in the property

            //JsonReferenceHandlingAttribute attr = GetAttribute<JsonReferenceHandlingAttribute>(state);

            //if (attr != null)
            //{
            //    handling = attr.Handling;
            //}
            //else
            //{
            handling = options.ReferenceHandling;
            //}

            switch (handling)
            {
                case ReferenceHandling.Error:
                case ReferenceHandling.Ignore:
                case ReferenceHandling.Serialize:
                    return ResolveReferenceLoop(handling, currentValue, ref state);
                case ReferenceHandling.Preserve:
                    return ResolvedReferenceHandling.Preserve;//return ResolvePreserveReference(currentValue, ref state, writer);

                default:
                    return ResolvedReferenceHandling.None;
            }
        }

        private static ResolvedReferenceHandling ResolveReferenceLoop(ReferenceHandling handling, object value, ref WriteStack state)
        {//If value is of Type struct we should not add it to the set.
            if (!state.AddStackReference(value))
            {
                if (handling == ReferenceHandling.Error)
                {
                    throw new JsonTestException("Invalid Reference Loop Detected!");
                }

                //if reference wasn't added to the set, it means it was already there, therefore we should ignore/serialize it BUT not remove it from the set in order to keep validating against further references.
                state.Current.KeepReferenceInSet = true;

                if (handling == ReferenceHandling.Serialize)
                {
                    return ResolvedReferenceHandling.None;
                }

                return ResolvedReferenceHandling.Ignore;
            }

            //New reference in the stack.
            return ResolvedReferenceHandling.None;
        }

        // Moved all the logic to WriteObjectOrArrayStart.
        private static bool ShouldWritePreservedReference(out int id, ref WriteStack state, object value = null) => !state.AddPreservedReference(value ?? state.Current.CurrentValue, out id);

        private static TAttribute GetAttribute<TAttribute>(WriteStack state) where TAttribute : Attribute
        {
            if (state.Current.JsonPropertyInfo != null)
            {
                //1. Check if property and has an attribute on top.
                Attribute attribute = JsonPropertyInfo.GetAttribute<TAttribute>(state.Current.JsonPropertyInfo.PropertyInfo); //this does not honours inheritance, be aware. I will implement it later?

                if (attribute != null)
                {
                    return (TAttribute)attribute;
                }

                //2. Check if property type has an attribute on top.
                return state.Current.JsonPropertyInfo.PropertyInfo.PropertyType.GetCustomAttribute<TAttribute>(inherit: false);//inheritance not supported.
            }
            //3. if JsonPropertyInfo is null We are looking at the root object or an array element, check if the Type has an attribute. //verify this is true
            else
            {
                return state.Current.JsonClassInfo.Type.GetCustomAttribute<TAttribute>();
            }
        }
    }

    /// <summary>
    /// test
    /// </summary>
    public class JsonTestException : JsonException
    {
        internal JsonTestException(string message) : base(message) { }
    }
}
