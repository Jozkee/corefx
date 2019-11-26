# Motivation

Currently there is no mechanism to prevent infinite looping in circular objects (while serializing) nor to preserve references that round-trip when using System.Text.Json.

This is a heavily requested feature since it is consider by many as a very common scenario, specially when serializing POCOs that came from an ORM Framework, such as Entity Framework; even though the JSON specification does not support reference loops by default. Therefore, this will be implemented as an opt-in feature (for both serialization and deserialization).

The current solution to deal with reference loops is to rely in MaxDepth and throw a JsonException after it is exceeded. Now, this is a decent and cheap solution but we will also offer other not-so-cheap options to deal with this problem while keeping the current one in order to not affect the out-of-the-box performance.

# Prior art

Json.Net contains settings that you can enable to deal with such problems.
* For Serialization:
  * [`ReferenceLoopHandling`](https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_ReferenceLoopHandling.htm)
  * [`PreserveReferencesHandling`](https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_PreserveReferencesHandling.htm)
* For Deserialization:
  * [`MetadataPropertyHandling`](https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_MetadataPropertyHandling.htm)

When using `ReferenceLoopHandling.Ignore`, other objects that were already seen on the current graph branch will be ignored on serialization.

When using `PreserveReferencesHandling.All` you are signaling that your resulting JSON will contain *metadata* properties `$ref`, `$id` and `$values` which are going to act as reference identifiers (`$id`) and pointers (`$ref`). 
Now, to read back those references, you have to use `MetadataPropertyHandling.Default` to indicate that *metadata* is expected in the payload passed to the `Deserialize` method.


# Proposal

```cs
namespace System.Text.Json.Serialization
{
    /// <summary>
    /// This class defines the various ways the <see cref="JsonSerializer"/> can deal with references on Serialization and Deserialization.
    /// </summary>
    public sealed class ReferenceHandling
    {
        //built-in options.
        public static ReferenceHandling Default => new ReferenceHandling(PreserveReferencesHandling.None);
        public static ReferenceHandling Preserve => new ReferenceHandling(PreserveReferencesHandling.All);

        // For future, change to public if needed.
        internal PreserveReferencesHandling PreserveHandlingOnSerialize => _preserveHandlingOnSerialize;
        internal PreserveReferencesHandling PreserveHandlingOnDeserialize => _preserveHandlingOnDeserialize;
        internal ReferenceLoopHandling LoopHandling => _loopHandling;

        private PreserveReferencesHandling _preserveHandlingOnSerialize;
        private PreserveReferencesHandling _preserveHandlingOnDeserialize;
        private ReferenceLoopHandling _loopHandling;

        public ReferenceHandling(PreserveReferencesHandling handling) : this(handling, handling, ReferenceLoopHandling.Error) { }

        // For future, someone may want to define their own custom Handler with different behaviors of PreserveReferenceHandling on Serialize vs Deserialize and another ReferenceLoopHandling, such as ignore if added in a future.
        private ReferenceHandling(PreserveReferencesHandling preserveHandlingOnSerialize, PreserveReferencesHandling preserveHandlingOnDeserialize, ReferenceLoopHandling loopHandling)
        {
            _preserveHandlingOnSerialize = preserveHandlingOnSerialize;
            _preserveHandlingOnDeserialize = preserveHandlingOnDeserialize;
            _loopHandling = loopHandling;
        }
    }

    internal enum ReferenceLoopHandling
    {
        Error = 0,
        // For future if requested by the community.
        // Ignore = 1,
    }

    public enum PreserveReferencesHandling
    {
        None = 0,
        All = 1,
        // For future if requested by the community.
        // Objects = 2,
        // Arrays = 3,
    }
}

namespace System.Text.Json
{
    public partial class JsonSerializerOptions
    {
        public ReferenceHandling ReferenceHandling { get; set; } = ReferenceHandling.Default;
    }
}
```
For System.Text.Json, the goal is to stick to the same *metadata* semantics for preserve from Json.Net and provide a similar usage in `JsonSerializerOptions` that encompasses the needed options (i.e. provide reference preservation).

This API is exposing the `ReferenceHandling` property as a class, to be extensible in the future; and provide built-in static instances of `Default` and `Preserve` that are useful to enable the most common behaviors by just setting those in `JsonSerializerOptions.ReferenceHandling`.

With `ReferenceHandling` being a class, we can exclude things that, as of now, we are not sure are required and add them later based on customer feedback. For example, the `Object` and `Array` granularity of `Newtonsoft.Json's` ``PreserveReferencesHandling` or the `ReferenceLoopHandling.Ignore` option.

In the future, we might also be able to extend the deserialization behaviors, in order to provide the granularity of Serialization on Deserialization. We could also provide ways to customize the serialization and deserialization behavior independently (i.e: someone may want to emit payloads with preserved references but they do not want to read them).

# Examples

Having the following class:
```cs
class Employee 
{ 
    string Name { get; set; }
    Employee Manager { get; set; }
    List<Employee> Subordinates { get; set; }
}
```

## Using Preserve on Serialize
On System.Text.Json:
```cs
public static void WritePreservingReference()
{
    var bob = new Employee { Name = "Bob" };
    var angela = new Employee { Name = "Angela" };

    angela.Manager = bob;
    bob.Subordinates = new List<Employee>{ angela };

    var options = new JsonSerializerOptions
    {
        ReferenceHandling = ReferenceHandling.Preserve
        WriteIndented = true,
    };

    string json = JsonSerializer.Serialize(angela, options);
    Console.Write(json);
}
```

On Newtonsoft's Json.Net:
```cs
public static void WritePreservingReference()
{
    var bob = new Employee { Name = "Bob" };
    var angela = new Employee { Name = "Angela" };

    angela.Manager = bob;
    bob.Subordinates = new List<Employee>{ angela };

    var settings = new JsonSerializerSettings
    {
        PreserveReferencesHandling = PreserveReferencesHandling.All
        Formatting = Formatting.Indented 
    };

    string json = JsonConvert.SerializeObject(angela, settings);
    Console.Write(json);
}
```

Output:
```jsonc
{
    "$id": "1",
    "Name": "Angela",
    "Manager": {
        "$id": "2",
        "Name": "Bob",
        "Subordinates": { //Note how the Subordinates' square braces are replaced with curly braces in order to include $id and $values properties, $values will now hold whatever value was meant for the Subordinates list.
            "$id": "3",
            "$values": [
                {  //Note how this object denotes reference to Angela that was previously serialized.
                    "$ref": "1"
                }
            ]
        }            
    }
}
```

## Using Preserve on Deserialize
On System.Text.Json:
```cs
public static void ReadJsonWithPreservedReferences(){
    string json = 
    @"{
        ""$id"": ""1"",
        ""Name"": ""Angela"",
        ""Manager"": {
            ""$id"": ""2"",
            ""Name"": ""Bob"",
            ""Subordinates"": {
                ""$id"": ""3"",
                ""$values"": [
                    { 
                        ""$ref"": ""1"" 
                    }
                ]
            }            
        }
    }";

    var options = new JsonSerializerOptions
    {
        ReferenceHandling = ReferenceHandling.Preserve
    };

    Employee angela = JsonSerializer.Deserialize<Employee>(json, options);
    Console.WriteLine(object.ReferenceEquals(angela, angela.Manager.Subordinates[0])); //prints: true.
}
```

On Newtonsoft's Json.Net:
```cs
public static void ReadJsonWithPreservedReferences(){
    string json = 
    @"{
        ""$id"": ""1"",
        ""Name"": ""Angela"",
        ""Manager"": {
            ""$id"": ""2"",
            ""Name"": ""Bob"",
            ""Subordinates"": {
                ""$id"": ""3"",
                ""$values"": [
                    { 
                        ""$ref"": ""1"" 
                    }
                ]
            }            
        }
    }";

    var options = new JsonSerializerSettings
    {
        MetadataPropertyHanding = MetadataPropertyHandling.Default //Json.Net reads metadata by default, just setting the option for ilustrative purposes.
    };

    Employee angela = JsonConvert.DeserializeObject<Employee>(json, settings);
    Console.WriteLine(object.ReferenceEquals(angela, angela.Manager.Subordinates[0])); //prints: true.
}
```

# Ground rules
## Unsupported types
Basically, any type that uses a `EnumerableConverter` and tries to be preserved will throw.

* **Immutable types**: i.e: `ImmutableList` and `ImmutableDictionary`
* **System.Array**

Aside from above, the Deserializer will throw when a CLR value type is passed as preserved when `ReferenceHandling.Preserve` is set. 

As a rule of thumb, we throw on all cases where the JSON payload being read contains any metadata that is impossible to create with the `JsonSerializer` (i.e. it was hand modified). However, this conflicts with feature parity in Newtonsoft.Json; those scenarios are described below.

## Reference objects ($ref)

* Regular property **before** `$ref`.
  * **Json.Net**: `$ref` is ignored if a regular property is previously found in the object.
  * **S.T.Json**: Throw - Reference objects cannot contain other properties.

```json
{
    "$id": "1",
    "Name": "Angela",
    "ReportsTo": {
        "Name": "Bob",
        "$ref": "1"
    }
}
```

* Regular property **after** `$ref`.
  * **Json.Net**: Throw - Additional content found in JSON reference object.
  * **S.T.Json**: Throw - Reference objects cannot contain other properties.

```json
{
    "$id": "1",
    "Name": "Angela",
    "ReportsTo":{
        "$ref": "1",
        "Name": "Angela" 
    }
}
```
 
* Metadata property **before** `$ref`:
  * **Json.Net**: `$id` is disregarded and the reference is set.
  * **S.T.Json**: Throw - Reference objects cannot contain other properties.
```json
{
    "$id": "1",
    "Name": "Angela",
    "ReportsTo": {
        "$id": "2",
        "$ref": "1"
    }
}
```

* Metadata property **after** `$ref`:
  * **Json.Net**: Throw with the next message: 'Additional content found in JSON reference object'.
  * **S.T.Json**: Throw - Reference objects cannot contain other properties.
```json
{
    "$id": "1",
    "Name": "Angela",
    "ReportsTo": {
        "$ref": "1",
        "$id": "2"
    }
}
```
 
* Reference object is before preserved object (or preserved object was never spotted):
  * **Json.Net**: Reference object evaluates as `null`.
  * **S.T.Json**: Reference object evaluates as `null`.
```json
[
    {
        "$ref": "1"
    },
    {
        "$id": "1",
        "Name": "Angela"
    }
]
```

## Preserved objects ($id)

* Having more than one `$id` in the same object:
  * **Json.Net**: last one wins, in the example, the reference object evaluates to `null` (if `$ref` would be `"2"`, it would evaluate to itself).
  * **S.T.Json**: Throw - Object already defines a reference identifier.
```json
{
    "$id": "1",
    "$id": "2",
    "Name": "Angela",
    "ReportsTo": {
        "$ref": "1"
    }
}
```

* `$id` is not the first property:
  * **Json.Net**: Object is not preserved and cannot be referenced, therefore any reference to it would evaluate as null.

  * **S.T.Json**: We can handle the `$id` not being the first property since we store the reference at the moment we spot the property, I don't think we should throw but keep in mind that this is not a normal payload produced by the serializer.
```json
{
    "Name": "Angela",
    "$id": "1",
    "ReportsTo": {
        "$ref": "1"
    }
}
```

* `$id` is duplicated (not necessarily nested):
  * **Json.Net**: Throws - Error reading object reference '1'- Inner Exception: ArgumentException: A different value already has the Id '1'.
  * **S.T.Json**: Throws - Duplicated id found while preserving reference.
```json
[
    {
        "$id": "1",
        "Name": "Angela"
    },
    {
        "$id": "1",
        "Name": "Bob"
    }
]
```

## Preserved arrays
A regular array is `[ elem1, elem2 ]`.
A preserved array is written in the next format `{ "$id": "1", "$values": [ elem1, elem2 ] }`

* Preserved array does not contain any metadata:
  * **Json.Net**: Throws - Cannot deserialize the current JSON object into type 'System.Collections.Generic.List`1
  * **S.T.Json**: Throw - Preserved array $values property was not present or its value is not an array.

  ```json
  {}
  ```

* Preserved array only contains $id:
  * **Json.Net**: Throws - Cannot deserialize the current JSON object into type 'System.Collections.Generic.List`1
  * **S.T.Json**: Throw - Preserved array $values property was not present or its value is not an array.

  ```json
  {
      "$id": "1"
  }
  ```

* Preserved array only contains `$values`:
  * **Json.Net**: Does not throw and the payload evaluates to the array in the property.
  * **S.T.Json**: Throw - Preserved arrays cannot lack an identifier.

  ```json
  {
      "$values": []
  }
  ```

* Preserved array $values property contains null
  * **Json.Net**: Throw - Unexpected token while deserializing object: EndObject. Path ''.
  * **S.T.Json**: Throw - Preserved array $values property was not present or its value is not an array.

  ```json
  {
      "$id": "1",
      "$values": null
  }
  ```

* Preserved array $values property contains value
  * **Json.Net**: Unexpected token while deserializing object: EndObject. Path ''.
  * **S.T.Json**: Throw - The JSON value could not be converted to TArray. Path: $.$values

  ```json
  {
      "$id": "1",
      "$values": 1
  }
  ```

* Preserved array $values property contains object
  * **Json.Net**: Unexpected token while deserializing object: EndObject. Path ''.
  * **S.T.Json**: Throw - The property is already part of a preserved array object, cannot be read as a preserved array.

  ```json
  {
      "$id": "1",
      "$values": {}
  }
  ```

# Notes

1. MaxDepth validation will not be affected by `ReferenceHandling.Preserve`.
2. We are merging the Json.Net types [`ReferenceLoopHandling`]("https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_ReferenceLoopHandling.htm") and [`PreserveReferencesHandling`]("https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_PreserveReferencesHandling.htm") (we are also not including the granularity on this one) into one single class; `ReferenceHandling`.
3. While Immutable types and `System.Array`s can be Serialized with Preserve semantics, they will not be supported when trying to Deserialize them as a reference.
4. Value types, such as structs that contain preserve semantics, will not be supported when Deserialized as well.
5. Additional features, such as Converter support, `ReferenceResolver`, `JsonPropertyAttribute.IsReference` and `JsonPropertyAttribute.ReferenceLoopHandling`,  that build on top of `ReferenceLoopHandling` and `PreserveReferencesHandling` were considered but they can be added in the future based on customer requests.
6. We are still looking for evidence that backs up supporting `ReferenceHandling.Ignore`, this option will not ship if said evidence is not found.
