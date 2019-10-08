// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Xunit;

namespace System.Text.Json.Tests
{
    internal class Employee
    {
        public string Name { get; set; }
        public Employee Manager { get; set; }
        public List<Employee> Subordinates { get; set; }
    }

    public static partial class ReferenceHandlingTestsDeserialize
    {        
        private static JsonSerializerOptions _deserializeOptions = new JsonSerializerOptions { ReadReferenceHandling = ReferenceHandlingOnDeserialize.PreserveDuplicates };
        //Pass cases
        [Fact]
        public static void DeserializeWithReferences() //Basic scenario.
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Name"": ""Angela"",
              ""Manager"": {
                    ""$ref"": ""1""
                }
            }";

            Employee angela = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);

            Assert.NotNull(angela);
            Assert.Same(angela, angela.Manager);
        }

        [Fact]
        public static void DeserializeEmptyArray() //Make sure the serializer can understand lists that were wrapped in braces.
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Subordinates"": {
                ""$id"": ""2"",
                ""$values"": []
              },
              ""Name"": ""Angela""
            }";

            Employee angela = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);

            Assert.NotNull(angela);
            Assert.NotNull(angela.Subordinates);
            Assert.Equal(0, angela.Subordinates.Count);

        }

        [Fact]
        public static void DeserializeClassDictionary()
        {
            string json =
            @"{
              ""555"": { ""$id"": ""1"", ""Name"": ""Angela"" },
              ""666"": { ""Name"": ""Bob"" },
              ""777"": { ""$ref"": ""1"" }
            }";

            Dictionary<string, Employee> directory = JsonSerializer.Deserialize<Dictionary<string, Employee>>(json, _deserializeOptions);

            Assert.NotNull(directory);
            Assert.Same(directory["555"], directory["777"]);

        }

        [Fact]
        public static void DeserializeWithConverterInNestedList()
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Subordinates"": {
                ""$id"": ""2"",
                ""$values"": [
                  { ""$ref"": ""1"" }
                ]
              },
              ""Name"": ""Angela"",
              ""Manager"": {
                    ""Subordinates"": { ""$ref"": ""2"" }
                }
            }";

            var options = new JsonSerializerOptions();
            options.ReadReferenceHandling = ReferenceHandlingOnDeserialize.PreserveDuplicates;
            options.Converters.Add(new MyConverter());

            Employee angela = JsonSerializer.Deserialize<Employee>(json, options);
        }

        //NOTE: If you implement a converter, you are on your own when handling metadata properties and therefore references.Newtonsoft does the same.
        //However; is there a way to recall preserved references previously found in the payload and to store new ones found in the converter's payload? that would be a cool enhancement.
        private class MyConverter : Serialization.JsonConverter<List<Employee>>
        {
            public override List<Employee> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                int startObjectCount = 0;
                int endObjectCount = 0;

                while (true)
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject:
                            startObjectCount++; break;
                        case JsonTokenType.EndObject:
                            endObjectCount++; break;
                    }

                    if (startObjectCount == endObjectCount)
                    {
                        break;
                    }

                    reader.Read();
                }

                return new List<Employee>();
            }

            public override void Write(Utf8JsonWriter writer, List<Employee> value, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }

        //This may be not needed, since objects hold JsonElements
        private class CustomDictionary : Dictionary<string, object>
        {

        }

        [Fact(Skip = "Waiting on https://github.com/dotnet/corefx/pull/41482")]//Disbale this until layomi merges his pr.
        public static void DeserializeCustomDictionary() // Angela["Self"] is of type JsonElement. which is very bad because JsonDocument was not in the scope for this.
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Self"": { ""$ref"": ""1"" }
            }";

            CustomDictionary angela = JsonSerializer.Deserialize<CustomDictionary>(json, _deserializeOptions);

            Assert.NotNull(angela);
        }

        private class MyDictionary : Dictionary<string, MyDictionary>
        {

        }

        [Fact(Skip = "Waiting on https://github.com/dotnet/corefx/pull/41482")]
        public static void DeserializeLoopIntoRecursiveDictionary() // Angela["Self"] is of type JsonElement. which is very bad because JsonDocument was not in the scope for this.
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Self"": { ""$ref"": ""1"" }
            }";

            MyDictionary angela = JsonSerializer.Deserialize<MyDictionary>(json, _deserializeOptions);

            Assert.NotNull(angela);
            Assert.Same(angela, angela["Self"]);

        }

        [Fact(Skip = "Waiting on https://github.com/dotnet/corefx/pull/41482")]
        public static void DeserializeIntoDictionary() // Angela["Self"] is of type JsonElement. which is very bad because JsonDocument was not in the scope for this.
        {
            string json =
            @"{                
              ""Self"": {  }
            }";

            MyDictionary angela = JsonSerializer.Deserialize<MyDictionary>(json, _deserializeOptions);

            Assert.NotNull(angela);
        }

        //base object tests, they will not work right now because those are deserialized as JsonElement instances.
        //UPDATE: they will remain as boxed JsonElements and nothing will be done to them.
        [Fact]
        public static void DeserializeDictionary() // Angela["Self"] is of type JsonElement. which is very bad because JsonDocument was not in the scope for this.
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Name"": ""Angela"",
              ""Self"": { ""$ref"": ""1"" },
              ""Other"": null
            }";

            //Should we throw when deserializing to an object type and having a metadata property?
            Dictionary<string, object> angela = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _deserializeOptions);

            Assert.NotNull(angela);
            object o = new JsonElement();
            Assert.Same(o.GetType(), angela["Self"].GetType());
        }

        [Fact(Skip = "JsonElement types are not supported for Preserve Reference Handling")]
        public static void DeserializeObject() //Deserialize anonymous object, those are created as JsonElement... and the deserializer internally calls JsonDocument.ParseValue.
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Name"": ""Angela"",
              ""Self"": { ""$ref"": ""1"" }
            }";

            object angela = JsonSerializer.Deserialize<object>(json, _deserializeOptions);

            Assert.NotNull(angela);
            JsonElement elem = (JsonElement)angela;
            var self = elem.GetProperty("Self");

            Assert.Same(angela.GetType(), ((object)new JsonElement()).GetType());

        }

        //This is being considered a ListConstructible which should not.
        [Fact]
        public static void DeserializeIntoArray()
        {
            //string json =
            //@"{
            //    ""$id"": ""1"",
            //    ""$values"": [
            //        {
            //            ""$id"": ""2"",
            //            ""Name"": ""Angela"",
            //            ""Manager"": {
            //                ""$id"": ""3"",
            //                ""Name"": ""Bob"",
            //                ""Subordinates"": { 
            //                    ""$ref"": ""1""
            //                }
            //            }
            //        },
            //        {
            //            ""$ref"": ""2""
            //        },
            //        {
            //            ""$ref"": ""3""
            //        }
            //    ]
            //}";
            string json =
            @"[
                {
                    ""$id"": ""2"",
                    ""Name"": ""Angela"",
                    ""Manager"": {
                        ""$id"": ""3"",
                        ""Name"": ""Bob"",
                        ""Subordinates"": { 
                            ""$ref"": ""1""
                        }
                    }
                },
                {
                    ""$ref"": ""2""
                },
                {
                    ""$ref"": ""3""
                }
            ]";

            Employee[] employees = JsonSerializer.Deserialize<Employee[]>(json, _deserializeOptions);
        }

        private class TestEmployee
        {
            public string Name { get; set; }
            public object Manager { get; set; }
        }

        [Fact(Skip = "JsonElement types are not supported for Preserve Reference Handling")]
        public static void DeserializeEmployeeWithObjectProperty() //Deserialize anonymous object, those are created as JsonElement... and the deserializer internally calls JsonDocument.ParseValue.
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Name"": ""Angela"",
              ""Manager"": { ""$ref"": ""1"" }
            }";

            object angela = JsonSerializer.Deserialize<TestEmployee>(json, _deserializeOptions);

            Assert.NotNull(angela); //angela.Manager (which  is object type) deserializes as a boxed JsonElement and metadata properties will be considered as regular properties.
            Assert.Same(angela, angela.GetType().GetProperty("Same").GetValue(angela));

        }


        [Fact]
        public static void DeserializeArrayWithCircularReference() //Reference within the list.
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Subordinates"": {
                ""$id"": ""2"",
                ""$values"": [
                  { ""$ref"": ""1"" }
                ]
              },
              ""Name"": ""Angela""
            }";

            Employee angela = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);

            Assert.NotNull(angela);
            Assert.NotNull(angela.Subordinates);
            Assert.Equal(1, angela.Subordinates.Count);
            Assert.Same(angela, angela.Subordinates[0]);
        }

        [Fact]
        public static void DeserializeDuplicatedArrayWithReference() //Reference to the list.
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Subordinates"": {
                ""$id"": ""2"",
                ""$values"": [
                  { ""$ref"": ""1"" }
                ]
              },
              ""Name"": ""Angela"",
              ""Manager"": {
                    ""Name"": ""Bob"",
                    ""Subordinates"": { ""$ref"": ""2"" }
                }
            }";

            Employee angela = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);

            Assert.NotNull(angela);
            Assert.Same(angela.Manager.Subordinates, angela.Subordinates);
        }
        [Fact]
        public static void DeserializeArrayWithInnerReference2() //the root element is a list.
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""$values"": [
                    {
                        ""$id"": ""2"",
                        ""Name"": ""Angela"",
                        ""Subordinates"": { 
                            ""$ref"": ""1""
                        }
                    },
                    {
                        ""$ref"": ""2""
                    },
                    {
                        ""$ref"": ""3""
                    }
                ]
            }";

            List<Employee> employees = JsonSerializer.Deserialize<List<Employee>>(json, _deserializeOptions);

            Assert.Same(employees, employees[0].Manager.Subordinates);
            Assert.Same(employees[1], employees[0]);
            Assert.Same(employees[2], employees[0].Manager);
        }

        [Fact]
        public static void DeserializeArrayWithInnerReference() //the root element is a list.
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""$values"": [
                    {
                        ""$id"": ""2"",
                        ""Name"": ""Angela"",
                        ""Manager"": {
                            ""$id"": ""3"",
                            ""Name"": ""Bob"",
                            ""Subordinates"": { 
                                ""$ref"": ""1""
                            }
                        }
                    },
                    {
                        ""$ref"": ""2""
                    },
                    {
                        ""$ref"": ""3""
                    }
                ]
            }";

            List<Employee> employees = JsonSerializer.Deserialize<List<Employee>>(json, _deserializeOptions);

            Assert.Same(employees, employees[0].Manager.Subordinates);
            Assert.Same(employees[1], employees[0]);
            Assert.Same(employees[2], employees[0].Manager);
        }

        [Fact]
        public static void DeserializeObjectIdNotFirstProperty()
        {
            string json =
            @"{
                ""Name"": ""Angela"",
                ""$id"": ""1"",
                ""Manager"": {
                    ""$ref"": ""1""
                }
            }";

            Employee angela = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);
            Assert.NotNull(angela);
            Assert.Same(angela, angela.Manager);
        }

        [Fact]
        public static void DeserializeObjectRefButNoId()
        {
            string json =
            @"{
                ""Name"": ""Angela"",
                ""Manager"": {
                    ""$ref"": ""1""
                }
            }";

            Employee angela = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);
            Assert.NotNull(angela);
            Assert.Null(angela.Manager);
        }

        [Fact]
        public static void DeserializeObjectRefBeforeId()
        {
            string json =
            @"{
                ""Name"": ""Angela"",
                ""Manager"": {
                    ""$ref"": ""1""
                },
                ""$id"": ""1""
            }";

            Employee angela = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);
            Assert.NotNull(angela);
            Assert.Null(angela.Manager);
        }

        #region Non-existent reference cases
        [Fact]
        public static void DeserializeObjectWithNonExistentRef()
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Name"": ""Angela"",
              ""Manager"": {
                    ""$ref"": ""2""
                },
              ""Subordinates"": {
                    ""$ref"": ""3""
                }                
            }";

            Employee angela = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);
            Assert.NotNull(angela);
            Assert.Null(angela.Manager);
            Assert.Null(angela.Subordinates);
        }

        [Fact]
        public static void DeserializeListWithNonExistentRef()
        {
            string json =
            @"[
                {
                    ""Name"": ""Angela"",
                    ""Manager"": {
                        ""Name"": ""Bob""
                    }
                },
                {
                    ""$ref"": ""2""
                },
                {
                    ""$ref"": ""3""
                }
            ]";

            List<Employee> subordinates = JsonSerializer.Deserialize<List<Employee>>(json, _deserializeOptions);
            Assert.NotNull(subordinates);
            Assert.Null(subordinates[1]);
            Assert.Null(subordinates[2]);
        }

        [Fact]
        public static void DeserializePreservedListWithNonExistentRef()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""$values"": [
                    {
                        ""Name"": ""Angela"",
                        ""Manager"": {
                            ""Name"": ""Bob"",
                            ""Subordinates"": { 
                                ""$ref"": ""1""
                            }
                        }
                    },
                    {
                        ""$ref"": ""2""
                    },
                    {
                        ""$ref"": ""3""
                    }
                ]
            }";

            List<Employee> subordinates = JsonSerializer.Deserialize<List<Employee>>(json, _deserializeOptions);
            Assert.NotNull(subordinates);
            Assert.Null(subordinates[1]);
            Assert.Null(subordinates[2]);
        }
        #endregion

        #region Throw cases
        //Immutables (should throw?)
        //Deserialize to an Immutable List
        [Fact]
        public static void DeserializeToImmutableListWithReferences()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""$values"": [
                    {
                        ""$id"": ""2"",
                        ""Name"": ""Angela"",
                        ""Sobordinates"": { ""$ref"": ""1"" }
                    }
                ]
            }";

            //this should throw
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ImmutableList<Employee>>(json, _deserializeOptions));
        }

        [Fact]
        public static void DeserializeToImmutableDictionaryWithReferences()
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Manager"": { ""$ref"": ""1"" }
            }";

            //this should throw.
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ImmutableDictionary<string, Employee>>(json, _deserializeOptions));
        }

        [Fact]
        public static void DeserializeObjectExtraPropertyOnRef()
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Name"": ""Angela"",
              ""Manager"": {
                    ""$ref"": ""1"",
                    ""Name"": ""Bob""
                }
            }";

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Employee>(json, _deserializeOptions));
        }

        [Fact]
        public static void DeserializeArrayValuesBeforeId()
        {
            string json =
            @"{
                ""$values"": [
                    {
                        ""$ref"": ""1""
                    }
                ],
                ""$id"": ""2""
            }";

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Employee>(json, _deserializeOptions));
        }

        [Fact]
        public static void DeserializeArrayRefIntoNonCollection()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Name"": ""Angela"",
                ""Subordinates"":{
                    ""$id"": ""2"",
                    ""$values"": []
                },
                ""Manager"": {
                    ""$ref"": ""2""
                }
            }";
            // not sure what it should say.
            Assert.Throws<InvalidCastException>(() => JsonSerializer.Deserialize<Employee>(json, _deserializeOptions));
        }

        [Fact]
        public static void DeserializePreservedReferenceAsArrayButIsObject()
        {
            string json =
            @"{
                ""Name"": ""Angela"",
                ""Manager"": {
                    ""$id"": ""1"",
                    ""$values"": []
                }
            }";
            // not sure what it should say.
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Employee>(json, _deserializeOptions));
        }
        #endregion

        // Not-sure behavior

        [Fact] // Not sure where to place this. Also not sure what's the expected for duplictaed ids. Most likely should throw.
        public static void DeerializeObjectWithMultipleIds()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Name"": ""Angela"",
                ""Manager"": {
                    ""$ref"": ""1""
                },
                ""$id"": ""2"",
                ""Subordinates"": [
                    {
                        ""$ref"": ""2""
                    }
                ]
            }";

            Employee angela = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);
        }
    }

    public static partial class ReferenceHandlingTestsDeserialize2
    {
        private static JsonSerializerOptions _deserializeOptions = new JsonSerializerOptions { ReadReferenceHandling = ReferenceHandlingOnDeserialize.PreserveDuplicates };
        //More tests

        [Fact (Skip = "Ignore this test.")] // Preserved list that contains itselt (array of arrays). Don't know how to deserialize this, tho.
        public static void Edge1()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""$values"": [
                    {
                        ""$ref"": ""1""
                    }
                ]
            }";

            List<Employee> employees = JsonSerializer.Deserialize<List<Employee>>(json, _deserializeOptions);
        }

        [Fact] // Preserved list that contains an empty employee and references to it. This test also validates that last brace for preserved lists must be discarded.
        public static void Edge2()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""$values"": [
                    {
                        ""$id"": ""2""
                    },
                    {
                        ""$ref"": ""2""
                    }
                ]
            }";

            List<Employee> employees = JsonSerializer.Deserialize<List<Employee>>(json, _deserializeOptions);
            
            Assert.Equal(2, employees.Count);
            Assert.Same(employees[0], employees[1]);
        }

        private class EmployeeWithContacts
        {
            public string Name { get; set; }
            public EmployeeWithContacts Manager { get; set; }
            public List<EmployeeWithContacts> Subordinates { get; set; }
            public Dictionary<string, EmployeeWithContacts> Contacts { get; set; }

        }

        #region Root Object
        [Fact] //Employee list as a property and then use reference to itself on nested Employee.
        public static void ObjectReferenceLoop()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Name"": ""Angela"",
                ""Manager"": {
                    ""$ref"": ""1""
                }
            }";

            Employee angela = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);
            Assert.Same(angela, angela.Manager);
        }

        [Fact] // Employee whose subordinates is a preserved list. EmployeeListEmployee
        public static void ObjectReferenceLoopInList()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Subordinates"": {
                    ""$id"": ""2"",
                    ""$values"": [
                        {
                            ""$ref"": ""1""
                        }
                    ]
                }
            }";

            Employee employee = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);
            Assert.Equal(1, employee.Subordinates.Count);
            Assert.Same(employee, employee.Subordinates[0]);
        }

        [Fact] // Employee whose subordinates is a preserved list. EmployeeListEmployee
        public static void ObjectReferenceLoopInDictionary()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Contacts"":{
                    ""$id"": ""2"",
                    ""Angela"":{
                        ""$ref"": ""1""
                    }
                }
            }";

            EmployeeWithContacts employee = JsonSerializer.Deserialize<EmployeeWithContacts>(json, _deserializeOptions);
            Assert.Same(employee, employee.Contacts["Angela"]);
        }

        [Fact] //Employee list as a property and then use reference to itself on nested Employee.
        public static void ObjectWithArrayReferencedDeeper()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Subordinates"": {
                    ""$id"": ""2"",
                    ""$values"": [
                        {
                            ""$id"": ""3"",
                            ""Name"": ""Angela"",
                            ""Subordinates"":{
                                ""$ref"": ""2""
                            }
                        }
                    ]
                }
            }";

            Employee employee = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);
            Assert.Same(employee.Subordinates, employee.Subordinates[0].Subordinates);
        }

        [Fact] //Employee Dictionary as a property and then use reference to itself on nested Employee.
        public static void ObjectWithDictionaryReferenceDeeper()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Contacts"": {
                    ""$id"": ""2"",
                    ""Angela"": {
                        ""$id"": ""3"",
                        ""Name"": ""Angela"",
                        ""Contacts"": {
                            ""$ref"": ""2""
                        }
                    }
                }
            }";

            EmployeeWithContacts employee = JsonSerializer.Deserialize<EmployeeWithContacts>(json, _deserializeOptions);
            Assert.Same(employee.Contacts, employee.Contacts["Angela"].Contacts);
        }
        #endregion

        #region Root Dictionary
        [Fact] //Employee list as a property and then use reference to itself on nested Employee.
        public static void DictionaryReferenceLoop()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Angela"": {
                    ""$id"": ""2"",
                    ""Name"": ""Angela"",
                    ""Contacts"": {
                        ""$ref"": ""1""
                    }
                }
            }";

            Dictionary<string, EmployeeWithContacts> dictionary = JsonSerializer.Deserialize<Dictionary<string, EmployeeWithContacts>>(json, _deserializeOptions);

            Assert.Same(dictionary, dictionary["Angela"].Contacts);
        }

        [Fact]
        public static void DictionaryReferenceLoopInList()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Angela"": {
                    ""$id"": ""2"",
                    ""Name"": ""Angela"",
                    ""Subordinates"": {
                        ""$id"": ""3"",
                        ""$values"": [
                            {
                                ""$id"": ""4"",
                                ""Name"": ""Bob"",
                                ""Contacts"": {
                                    ""$ref"": ""1""
                                }
                            }
                        ]
                    }
                }
            }";

            Dictionary<string, EmployeeWithContacts> dictionary = JsonSerializer.Deserialize<Dictionary<string, EmployeeWithContacts>>(json, _deserializeOptions);
            Assert.Same(dictionary, dictionary["Angela"].Subordinates[0].Contacts);
        }

        [Fact]
        public static void DicitionaryDuplicatedObject()
        {
            string json =
            @"{
              ""555"": { ""$id"": ""1"", ""Name"": ""Angela"" },
              ""556"": { ""Name"": ""Bob"" },
              ""557"": { ""$ref"": ""1"" }
            }";

            Dictionary<string, Employee> directory = JsonSerializer.Deserialize<Dictionary<string, Employee>>(json, _deserializeOptions);
            Assert.Same(directory["555"], directory["557"]);
        }

        [Fact] //This should not throw, since the references are in nested objects, not in the immutable dictionary itself.
        public static void ImmutableDictionaryPreserveNestedObjects()
        {
            string json =
            @"{
                ""Angela"": {
                    ""$id"": ""1"",
                    ""Name"": ""Angela"",
                    ""Subordinates"": {
                        ""$id"": ""2"",
                        ""$values"": [
                            {
                                ""$id"": ""3"",
                                ""Name"": ""Carlos"",
                                ""Manager"": {
                                    ""$ref"": ""1""
                                }
                            }
                        ]
                    }
                },
                ""Bob"": {
                    ""$id"": ""4"",
                    ""Name"": ""Bob""
                },
                ""Carlos"": {
                    ""$ref"": ""3""
                }
            }";

            ImmutableDictionary<string, Employee> dictionary = JsonSerializer.Deserialize<ImmutableDictionary<string, Employee>>(json, _deserializeOptions);
            Assert.Same(dictionary["Angela"], dictionary["Angela"].Subordinates[0].Manager);
            Assert.Same(dictionary["Carlos"], dictionary["Angela"].Subordinates[0]);
        }
        #endregion

        #region Root Array
        [Fact] // Preserved list that contains an employee whose subordinates is a reference to the root list.
        public static void ArrayNestedArray()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""$values"":[
                    {
                        ""$id"":""2"",
                        ""Name"": ""Angela"",
                        ""Subordinates"": {
                            ""$ref"": ""1""
                        }
                    }
                ]
            }";

            List<Employee> employees = JsonSerializer.Deserialize<List<Employee>>(json, _deserializeOptions);

            Assert.Same(employees, employees[0].Subordinates);
        }


        [Fact]
        public static void EmptyArray() //Make sure the serializer can understand lists that were wrapped in braces.
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Subordinates"": {
                ""$id"": ""2"",
                ""$values"": []
              },
              ""Name"": ""Angela""
            }";

            Employee angela = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);

            Assert.NotNull(angela);
            Assert.NotNull(angela.Subordinates);
            Assert.Equal(0, angela.Subordinates.Count);

        }

        [Fact]
        public static void ArrayWithDuplicates() //Make sure the serializer can understand lists that were wrapped in braces.
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""$values"":[
                    {
                        ""$id"": ""2"",
                        ""Name"": ""Angela""
                    },
                    {
                        ""$id"": ""3"",
                        ""Name"": ""Bob""
                    },
                    {
                        ""$ref"": ""2""
                    },
                    {
                        ""$ref"": ""3""
                    }
                ]
            }";

            List<Employee> employees = JsonSerializer.Deserialize<List<Employee>>(json, _deserializeOptions);
            Assert.Equal(4, employees.Count);
            Assert.Same(employees[0], employees[2]);
            Assert.Same(employees[1], employees[3]);

        }

        [Fact]
        public static void ArrayNotPreservedWithDuplicates() //Make sure the serializer can understand lists that were wrapped in braces.
        {
            string json =
            @"[
                {
                    ""$id"": ""2"",
                    ""Name"": ""Angela""
                },
                {
                    ""$id"": ""3"",
                    ""Name"": ""Bob""
                },
                {
                    ""$ref"": ""2""
                },
                {
                    ""$ref"": ""3""
                }
            ]";

            Employee[] employees = JsonSerializer.Deserialize<Employee[]>(json, _deserializeOptions);
            Assert.Equal(4, employees.Length);
            Assert.Same(employees[0], employees[2]);
            Assert.Same(employees[1], employees[3]);
        }
        #endregion

        #region Converter
        [Fact] //This only demonstrates that behavior with converters remain the same.
        public static void DeserializeWithListConverter()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Subordinates"": {
                    ""$id"": ""2"",
                    ""$values"": [
                        {
                            ""$ref"": ""1""
                        }
                    ]
                },
                ""Name"": ""Angela"",
                ""Manager"": {
                    ""Subordinates"": {
                        ""$ref"": ""2""
                    }
                }
            }";

            var options = new JsonSerializerOptions();
            options.ReadReferenceHandling = ReferenceHandlingOnDeserialize.PreserveDuplicates;
            options.Converters.Add(new MyConverter());

            Employee angela = JsonSerializer.Deserialize<Employee>(json, options);
        }

        //NOTE: If you implement a converter, you are on your own when handling metadata properties and therefore references.Newtonsoft does the same.
        //However; is there a way to recall preserved references previously found in the payload and to store new ones found in the converter's payload? that would be a cool enhancement.
        private class MyConverter : Serialization.JsonConverter<List<Employee>>
        {
            public override List<Employee> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                int startObjectCount = 0;
                int endObjectCount = 0;

                while (true)
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject:
                            startObjectCount++; break;
                        case JsonTokenType.EndObject:
                            endObjectCount++; break;
                    }

                    if (startObjectCount == endObjectCount)
                    {
                        break;
                    }

                    reader.Read();
                }

                return new List<Employee>();
            }

            public override void Write(Utf8JsonWriter writer, List<Employee> value, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }
        #endregion

        #region Null/non-existent reference
        [Fact]
        public static void ObjectNull() //Make sure the serializer can understand lists that were wrapped in braces.
        {
            string json =
            @"{
                ""$ref"": ""1""
            }";

            Employee employee = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);
            Assert.Null(employee);
        }

        [Fact]
        public static void ArrayNull() //Make sure the serializer can understand lists that were wrapped in braces.
        {
            string json =
            @"{
                ""$ref"": ""1""
            }";

            Employee[] array = JsonSerializer.Deserialize<Employee[]>(json, _deserializeOptions);
            Assert.Null(array);
        }

        [Fact]
        public static void DictionaryNull() //Make sure the serializer can understand lists that were wrapped in braces.
        {
            string json =
            @"{
                ""$ref"": ""1""
            }";

            Dictionary<string, Employee> dictionary = JsonSerializer.Deserialize<Dictionary<string, Employee>>(json, _deserializeOptions);
            Assert.Null(dictionary);
        }

        [Fact]
        public static void ArrayPropertyNull() //Make sure the serializer can understand lists that were wrapped in braces.
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Name"": ""Angela"",
                ""Manager"": {
                    ""$ref"": ""1""
                },
                ""Subordinates"": {
                    ""$ref"": ""2""
                }
            }";

            Employee angela = JsonSerializer.Deserialize<Employee>(json, _deserializeOptions);
            Assert.Null(angela.Subordinates);
        }

        // TODO: Add struct case where reference would evaluate as null; also, what does Json.Net does for that?
        #endregion

        #region Throw cases
        [Fact] // Paired with Json.Net 
        public static void PropertyAfterRef()
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Name"": ""Angela"",
              ""Manager"": {
                    ""$ref"": ""1"",
                    ""Name"": ""Bob""
                }
            }";

            Exception ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Employee>(json, _deserializeOptions));
            Assert.Equal("Properties other than $ref are not allowed in reference objects.", ex.Message);
        }

        [Fact] 
        public static void PropertyBeforeRef()
        {
            string json =
            @"{
              ""$id"": ""1"",
              ""Name"": ""Angela"",
              ""Manager"": {
                    ""Name"": ""Bob"",
                    ""$ref"": ""1""
                }
            }";

            Exception ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Employee>(json, _deserializeOptions));
            Assert.Equal("Properties other than $ref are not allowed in reference objects.", ex.Message);
        }

        //Immutables
        [Fact] // Paired with Json.Net 
        public static void ImmutableListTryPreserve()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""$values"": []
            }";

            //ImmutableList<Employee> list = JsonSerializer.Deserialize<ImmutableList<Employee>>(json, _deserializeOptions);

            Exception ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ImmutableList<Employee>>(json, _deserializeOptions));
            Assert.Equal("Cannot preserve references for types that are immutable.", ex.Message);
        }

        [Fact] // Paired with Json.Net 
        public static void ImmutableDictionaryTryPreserve()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Angela"": {}
            }";

            Exception ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ImmutableDictionary<string, Employee>>(json, _deserializeOptions));
            Assert.Equal("Cannot preserve references for types that are immutable.", ex.Message);
        }

        private class EmployeeWithImmutables
        {
            public string Name { get; set; }
            public EmployeeWithImmutables Manager { get; set; }
            public ImmutableList<EmployeeWithImmutables> Subordinates { get; set; }
            public ImmutableDictionary<string, EmployeeWithImmutables> Contacts { get; set; }
        }

        [Fact] // Paired with Json.Net 
        public static void ImmutableListAsPropertyTryPreserve()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Name"": ""Angela"",
                ""Subordinates"": {
                    ""$id"": ""2"",
                    ""$values"": []
                }
            }";

            Exception ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EmployeeWithImmutables>(json, _deserializeOptions));
            Assert.Equal("Cannot preserve references for types that are immutable.", ex.Message);
        }

        [Fact] // Paired with Json.Net 
        public static void ImmutableDictionaryAsPropertyTryPreserve()
        {
            string json =
            @"{
                ""$id"": ""1"",
                ""Name"": ""Angela"",
                ""Contacts"": {
                    ""$id"": ""2""
                }
            }";

            Exception ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EmployeeWithImmutables>(json, _deserializeOptions));
            Assert.Equal("Cannot preserve references for types that are immutable.", ex.Message);
        }
        #endregion
    }
}
