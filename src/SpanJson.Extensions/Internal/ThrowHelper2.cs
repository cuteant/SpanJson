using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using SpanJson.Internal;
using SpanJson.Linq;
using SpanJson.Utilities;

namespace SpanJson
{
    internal static class ThrowHelper2
    {
        #region -- Exception --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Exception GetException_Can_not_convert_null_into_non_nullable(object initialValue, Type targetType)
        {
            return new Exception("Can not convert null {0} into non-nullable {1}.".FormatWith(CultureInfo.InvariantCulture, initialValue.GetType(), targetType));
        }

        #endregion

        #region -- ArgumentException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_MustBe<T>()
        {
            return new ArgumentException($"Object must be of type {typeof(T).Name}.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Argument_is_not_a_JToken()
        {
            return new ArgumentException("Argument is not a JToken.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Source_value_must_be_a_JToken()
        {
            return new ArgumentException("Source value must be a JToken.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Property_does_not_have_a_getter()
        {
            return new ArgumentException("Property does not have a getter.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Property_does_not_have_a_setter()
        {
            return new ArgumentException("Property does not have a setter.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Accessed_JArray_values_with_invalid_key_value(object key)
        {
            return new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Accessed JArray values with invalid key value: {0}. Int32 array index expected.", MiscellaneousUtils.ToString(key)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Set_JArray_values_with_invalid_key_value(object key)
        {
            return new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Set JArray values with invalid key value: {0}. Int32 array index expected.", MiscellaneousUtils.ToString(key)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Accessed_JObject_values_with_invalid_key_value(object key)
        {
            return new ArgumentException("Accessed JObject values with invalid key value: {0}. Object property name expected.".FormatWith(CultureInfo.InvariantCulture, MiscellaneousUtils.ToString(key)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Set_JObject_values_with_invalid_key_value(object key)
        {
            return new ArgumentException("Set JObject values with invalid key value: {0}. Object property name expected.".FormatWith(CultureInfo.InvariantCulture, MiscellaneousUtils.ToString(key)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Target_type_is_not_a_value_type_or_a_non_abstract_class(Type targetType)
        {
            return new ArgumentException("Target type {0} is not a value type or a non-abstract class.".FormatWith(CultureInfo.InvariantCulture, targetType), nameof(targetType));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Could_not_cast_or_convert_from(Type initialType, Type targetType)
        {
            return new ArgumentException("Could not cast or convert from {0} to {1}.".FormatWith(CultureInfo.InvariantCulture, initialType?.ToString() ?? "{null}", targetType));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Cast<TTo>(JToken value)
        {
            throw GetArgumentException_Cast<TTo>(value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Cast<TTo>(JToken value)
        {
            return new ArgumentException($"Can not convert {JToken.GetType(value)} to {typeof(TTo).Name}.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Could_not_determine_JSON_object_type_for_type_JsonElement()
        {
            return new ArgumentException("Could not determine JSON object type for type 'JsonElement'.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Could_not_determine_JSON_object_type_for_type(object value)
        {
            return new ArgumentException("Could not determine JSON object type for type {0}.".FormatWith(CultureInfo.InvariantCulture, value.GetType()));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_ArrayIndex()
        {
            throw GetArgumentException();

            static ArgumentException GetArgumentException()
            {
                return new ArgumentException("arrayIndex is equal to or greater than the length of array.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_The_number_of_elements()
        {
            throw GetArgumentException();

            static ArgumentException GetArgumentException()
            {
                return new ArgumentException("The number of elements in the source JObject is greater than the available space from arrayIndex to the end of the destination array.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_The_specified_item_does_not_exist_in_this_KeyedCollection()
        {
            throw GetArgumentException();

            static ArgumentException GetArgumentException()
            {
                return new ArgumentException("The specified item does not exist in this KeyedCollection.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_Could_not_convert(Type objectType, JToken token, Exception ex)
        {
            Type enumType = objectType.IsEnum ? objectType : Nullable.GetUnderlyingType(objectType);
            return new ArgumentException("Could not convert '{0}' to {1}.".FormatWith(CultureInfo.InvariantCulture, (string)token, enumType.Name), ex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CanNotAdd(JToken o)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("Can not add {0} to {1}.".FormatWith(CultureInfo.InvariantCulture, o.GetType(), typeof(JObject)));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CanNotAdd(JToken o, JContainer container)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("Can not add {0} to {1}.".FormatWith(CultureInfo.InvariantCulture, o.GetType(), container.GetType()));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CanNotAddProperty(JProperty newProperty)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("Can not add property {0} to {1}. Property with the same name already exists on object.".FormatWith(CultureInfo.InvariantCulture, newProperty.Name, typeof(JObject)));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_JsonDocumentDoesNotSupportComments(ExceptionArgument paramName)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException(SR.JsonDocumentDoesNotSupportComments, ThrowHelper.GetArgumentName(paramName));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Object_serialized_to_JArray_instance_expected(JTokenType tokenType)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("Object serialized to {0}. JArray instance expected.".FormatWith(CultureInfo.InvariantCulture, tokenType));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Object_serialized_to_JObject_instance_expected(JTokenType tokenType)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("Object serialized to {0}. JObject instance expected.".FormatWith(CultureInfo.InvariantCulture, tokenType));
            }
        }

        #endregion

        #region -- ArgumentOutOfRangeException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentOutOfRangeException GetArgumentOutOfRangeException_UnexpectedValueType(JTokenType valueType)
        {
            return MiscellaneousUtils.CreateArgumentOutOfRangeException(nameof(valueType), valueType, "Unexpected value type: {0}".FormatWith(CultureInfo.InvariantCulture, valueType)); ;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Unexpected_merge_array_handling_when_merging_JSON()
        {
            throw GetArgumentException();

            static ArgumentOutOfRangeException GetArgumentException()
            {
                return new ArgumentOutOfRangeException("settings", "Unexpected merge array handling when merging JSON.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Index_must_be_within_the_bounds_of_the_List(ExceptionArgument argument)
        {
            throw GetArgumentException();
            ArgumentOutOfRangeException GetArgumentException()
            {
                return new ArgumentOutOfRangeException(ThrowHelper.GetArgumentName(argument), "Index must be within the bounds of the List.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Index()
        {
            throw GetArgumentException();

            static ArgumentOutOfRangeException GetArgumentException()
            {
                return new ArgumentOutOfRangeException("index", "Index is less than 0.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_ArrayIndex()
        {
            throw GetArgumentException();

            static ArgumentOutOfRangeException GetArgumentException()
            {
                return new ArgumentOutOfRangeException("arrayIndex", "arrayIndex is less than 0.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Index_is_equal_to_or_greater_than_Count()
        {
            throw GetArgumentException();

            static ArgumentOutOfRangeException GetArgumentException()
            {
                return new ArgumentOutOfRangeException("index", "Index is equal to or greater than Count.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_JsonDocumentDoesNotSupportComments(ExceptionArgument argument)
        {
            throw GetArgumentException();
            ArgumentOutOfRangeException GetArgumentException()
            {
                return new ArgumentOutOfRangeException(ThrowHelper.GetArgumentName(argument), SR.JsonDocumentDoesNotSupportComments);
            }
        }

        #endregion

        #region -- JsonSerializationException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Newtonsoft.Json.JsonSerializationException GetJsonSerializationException<T>()
        {
            return new Newtonsoft.Json.JsonSerializationException($"Expected {typeof(T).Name} object value");
        }

        #endregion

        #region -- InvalidOperationException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static InvalidOperationException GetInvalidOperationException_Unexpected_conversion_result()
        {
            return new InvalidOperationException("Unexpected conversion result.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static InvalidOperationException GetInvalidOperationException_Can_not_convert_from_to(object initialValue, Type targetType)
        {
            return new InvalidOperationException("Can not convert from {0} to {1}.".FormatWith(CultureInfo.InvariantCulture, initialValue.GetType(), targetType));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_The_parent_is_missing()
        {
            throw GetInvalidOperationException();

            static InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("The parent is missing.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_Cannot_change_during_a_collection_change_event(JContainer container)
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Cannot change {0} during a collection change event.", container.GetType()));
            }
        }

        #endregion

        #region -- IndexOutOfRangeException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IndexOutOfRangeException GetIndexOutOfRangeException()
        {
            return new IndexOutOfRangeException();
        }

        #endregion

        #region -- KeyNotFoundException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static KeyNotFoundException GetKeyNotFoundException()
        {
            return new KeyNotFoundException();
        }

        #endregion

        #region -- FormatException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static FormatException GetFormatException()
        {
            return new FormatException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowFormatException_Guid()
        {
            throw GetException();
            static FormatException GetException()
            {
                return new FormatException(SR.FormatGuid);
            }
        }

        #endregion

        #region -- InvalidCastException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static InvalidCastException GetInvalidCastException<T>(JToken token)
        {
            return new InvalidCastException(string.Format(CultureInfo.InvariantCulture, "Cannot cast {0} to {1}.", token.GetType(), typeof(T)));
        }

        #endregion

        #region -- JsonReaderException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonReaderException GetJsonReaderException_Error_reading_JArray_from_JsonReader()
        {
            return new JsonReaderException("Error reading JArray from JsonReader.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonReaderException GetJsonReaderException_Error_reading_JObject_from_JsonReader()
        {
            return new JsonReaderException("Error reading JObject from JsonReader.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonReaderException GetJsonReaderException_Error_reading_JContainer_from_JsonReader()
        {
            return new JsonReaderException("Error reading JContainer from JsonReader.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonReaderException GetJsonReaderException_Error_reading_JValue_from_JsonReader()
        {
            return new JsonReaderException("Error reading JValue from JsonReader.");
        }

        #endregion

        #region -- JsonException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonException GetJsonException_Cannot_add_or_remove_items_from_JProperty()
        {
            return new JsonException(string.Format(CultureInfo.InvariantCulture, "Cannot add or remove items from {0}.", typeof(JProperty)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonException GetJsonException_Path_ended_with_open_query()
        {
            return new JsonException("Path ended with open query.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonException GetJsonException_Path_ended_with_open_indexer()
        {
            return new JsonException("Path ended with open indexer.");
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Path_ended_with_open_indexer()
        {
            throw GetJsonException_Path_ended_with_open_indexer();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonException GetJsonException_Path_ended_with_an_open_string()
        {
            return new JsonException("Path ended with an open string.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonException GetJsonException_Path_ended_with_an_open_regex()
        {
            return new JsonException("Path ended with an open regex.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonException GetJsonException_Could_not_read_query_operator()
        {
            return new JsonException("Could not read query operator.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonException GetJsonException_Unknown_escape_character(char currentChar)
        {
            return new JsonException(@"Unknown escape character: \" + currentChar);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonException GetJsonException(string message)
        {
            return new JsonException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException(string message)
        {
            throw GetJsonException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Cannot_have_multiple_values_JProperty()
        {
            throw GetJsonException();

            static JsonException GetJsonException()
            {
                return new JsonException(string.Format(CultureInfo.InvariantCulture, "{0} cannot have multiple values.", typeof(JProperty)));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Path_returned_multiple_tokens()
        {
            throw GetJsonException();

            static JsonException GetJsonException()
            {
                return new JsonException("Path returned multiple tokens.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Step_cannot_be_zero()
        {
            throw GetJsonException();

            static JsonException GetJsonException()
            {
                return new JsonException("Step cannot be zero.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Array_index_expected()
        {
            throw GetJsonException();

            static JsonException GetJsonException()
            {
                return new JsonException("Array index expected.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Path_ended_with_open_query()
        {
            throw GetJsonException_Path_ended_with_open_query();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Unexpected_end_while_parsing_path()
        {
            throw GetJsonException();

            static JsonException GetJsonException()
            {
                return new JsonException("Unexpected end while parsing path.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Index_not_valid_on(JToken token)
        {
            throw GetJsonException();
            JsonException GetJsonException()
            {
                return new JsonException(string.Format(CultureInfo.InvariantCulture, "Index * not valid on {0}.", token.GetType().Name));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Array_slice_is_not_valid_on(JToken token)
        {
            throw GetJsonException();
            JsonException GetJsonException()
            {
                return new JsonException(string.Format(CultureInfo.InvariantCulture, "Array slice is not valid on {0}.", token.GetType().Name));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Array_slice_of_to_returned_to_results(int? start, int? end)
        {
            throw GetJsonException();
            JsonException GetJsonException()
            {
                return new JsonException(string.Format(CultureInfo.InvariantCulture, "Array slice of {0} to {1} returned no results.",
                    start is object ? start.GetValueOrDefault().ToString(CultureInfo.InvariantCulture) : "*",
                    end is object ? end.GetValueOrDefault().ToString(CultureInfo.InvariantCulture) : "*"));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Property_does_not_exist_on_JObject(string name)
        {
            throw GetJsonException();
            JsonException GetJsonException()
            {
                return new JsonException(string.Format(CultureInfo.InvariantCulture, "Property '{0}' does not exist on JObject.", name));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Property_not_valid_on(string name, JToken token)
        {
            throw GetJsonException();
            JsonException GetJsonException()
            {
                return new JsonException(string.Format(CultureInfo.InvariantCulture, "Property '{0}' not valid on {1}.", name ?? "*", token.GetType().Name));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Property_not_valid_on(List<string> names, JToken token)
        {
            throw GetJsonException();
            JsonException GetJsonException()
            {
                return new JsonException(string.Format(CultureInfo.InvariantCulture, "Properties {0} not valid on {1}.", string.Join(", ", names.Select(n => "'" + n + "'")), token.GetType().Name));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Index_outside_the_bounds_of_JArray(int index)
        {
            throw GetJsonException();
            JsonException GetJsonException()
            {
                return new JsonException(string.Format(CultureInfo.InvariantCulture, "Index {0} outside the bounds of JArray.", index));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Index_not_valid_on(int index, JToken token)
        {
            throw GetJsonException();
            JsonException GetJsonException()
            {
                return new JsonException(string.Format(CultureInfo.InvariantCulture, "Index {0} not valid on {1}.", index, token.GetType().Name));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonException GetJsonException_New_item_to_be_added_to_collection_must_be_compatible_with()
        {
            return new JsonException(string.Format(CultureInfo.InvariantCulture, "New item to be added to collection must be compatible with {0}.", typeof(JToken)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonException GetJsonException_Could_not_determine_new_value_to_add_to(JContainer container)
        {
            return new JsonException(string.Format(CultureInfo.InvariantCulture, "Could not determine new value to add to '{0}'.", container.GetType()));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Unexpected_character_while_parsing_path(in ReadOnlySpan<char> expression, int lastCharacterIndex)
        {
            throw GetJsonException_Unexpected_character_while_parsing_path(expression, lastCharacterIndex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static JsonException GetJsonException_Unexpected_character_while_parsing_path(in ReadOnlySpan<char> expression, int lastCharacterIndex)
        {
            return new JsonException("Unexpected character while parsing path: " + expression[lastCharacterIndex]);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Unexpected_character_following_indexer(char currentChar)
        {
            throw GetJsonException();
            JsonException GetJsonException()
            {
                return new JsonException("Unexpected character following indexer: " + currentChar);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Unexpected_character_while_parsing_path_indexer(char currentCharacter)
        {
            throw GetJsonException();
            JsonException GetJsonException()
            {
                return new JsonException("Unexpected character while parsing path indexer: " + currentCharacter);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowJsonException_Unexpected_character_while_parsing_path_indexer(in ReadOnlySpan<char> expression, int currentIndex)
        {
            throw GetJsonExceGetUnexpected_character_while_parsing_path_indexerption(expression, currentIndex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static JsonException GetJsonExceGetUnexpected_character_while_parsing_path_indexerption(in ReadOnlySpan<char> expression, int currentIndex)
        {
            return new JsonException("Unexpected character while parsing path indexer: " + expression[currentIndex]);
        }

        #endregion
    }
}
