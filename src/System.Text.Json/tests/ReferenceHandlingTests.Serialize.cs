// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using Xunit;

namespace System.Text.Json.Tests
{
    public static partial class ReferenceHandlingTests
    {
        //[JsonReferenceHandling(ReferenceHandling.Error)]
        private class Employee
        {
            //[Serialization.JsonPropertyName("todo")]//todo: add a referencehandling attribute.
            public string Name { get; set; }

            public Employee Manager { get; set; }

            public List<Employee> Subordinates { get; set; }
        }

        //Base scenarios
        [Theory]
        [InlineData(ReferenceHandling.Error)]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        public static void ObjectLoop(ReferenceHandling referenceHandling)
        {
            Employee angela = new Employee();
            angela.Manager = angela;

            TestSerialize(angela, referenceHandling, out string outputExpected, out string outputActual, out bool failExpected, out bool failActual);

            Assert.Equal(outputExpected, outputActual);
            Assert.Equal(failExpected, failActual);
        }

        [Theory]
        [InlineData(ReferenceHandling.Error)]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        //Since we are using state.Current.CurrentValue, this used to fail when the dictionary was a property within the object.
        public static void ObjectWithDictionary(ReferenceHandling referenceHandling)
        {
            object obj = new
            {
                Employees = new Dictionary<string, Employee>
                {
                    { "Angela", new Employee { Name = "Angela" } }
                }
            };

            TestSerialize(obj, referenceHandling, out string outputExpected, out string outputActual, out bool failExpected, out bool failActual);

            Assert.Equal(outputExpected, outputActual);
            Assert.Equal(failExpected, failActual);
        }

        [Theory]
        [InlineData(ReferenceHandling.Error)]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        //Since we are using state.Current.CurrentValue, this used to fail when the list was a property within the object.
        public static void ObjectWithList(ReferenceHandling referenceHandling)
        {
            object obj = new
            {
                Employees = new List<Employee> { new Employee { Name = "Angela" } }
            };

            TestSerialize(obj, referenceHandling, out string outputExpected, out string outputActual, out bool failExpected, out bool failActual);

            Assert.Equal(outputExpected, outputActual);
            Assert.Equal(failExpected, failActual);
        }

        [Theory]
        [InlineData(ReferenceHandling.Error)]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        // This was failing because a list or dictionary within an object is never pushed into the stack, therefore it was never removed from the _referenceLoopStack.
        public static void ObjectWithDuplicatedList(ReferenceHandling referenceHandling)
        {
            List<object> list = new List<object>();
            object obj = new
            {
                a = list,
                b = list,
            };

            TestSerialize(obj, referenceHandling, out string outputExpected, out string outputActual, out bool failExpected, out bool failActual);

            Assert.Equal(outputExpected, outputActual);
            Assert.Equal(failExpected, failActual);
        }

        [Theory]
        [InlineData(ReferenceHandling.Error)]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        // Now this one fails because a list with more than 1 elem. will not call HandleEnumerable once but several times until it enumerates them all after the first call, enumerable remains null.
        public static void ObjectWithDuplicatedListWithValues(ReferenceHandling referenceHandling)
        {
            List<int> list = new List<int> { 1, 2, 3 };
            object obj = new
            {
                a = list,
                b = list,
            };

            TestSerialize(obj, referenceHandling, out string outputExpected, out string outputActual, out bool failExpected, out bool failActual);

            Assert.Equal(outputExpected, outputActual);
            Assert.Equal(failExpected, failActual);
        }

        [Theory]
        [InlineData(ReferenceHandling.Error)]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        public static void ArrayLoop(ReferenceHandling referenceHandling)
        {
            Employee angela = new Employee();
            angela.Subordinates = new List<Employee> { angela };

            TestSerialize(angela, referenceHandling, out string outputExpected, out string outputActual, out bool failExpected, out bool failActual);

            Assert.Equal(outputExpected, outputActual);
            Assert.Equal(failExpected, failActual);
        }


        [Theory]
        [InlineData(ReferenceHandling.Error)]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        public static void NestedArray(ReferenceHandling referenceHandling)
        {
            List<object> array = new List<object>();
            array.Add(array);

            TestSerialize(array, referenceHandling, out string outputExpected, out string outputActual, out bool failExpected, out bool failActual);

            Assert.Equal(outputExpected, outputActual);
            Assert.Equal(failExpected, failActual);
        }
        //Base scenarios

        [Fact]//Check objects are correctly added/removed from the Hashset.
        public static void ObjectUnevenTimesUsingIgnore()
        {
            Employee angela = new Employee();
            Employee bob = new Employee();

            bob.Manager = angela;
            angela.Manager = angela;

            List<Employee> employees = new List<Employee> { bob, bob, bob };


            TestSerialize(employees, default, out string outputExpected, out string outputActual, out bool failExpected, out bool failActual);

            Assert.Equal(outputExpected, outputActual);
            Assert.Equal(failExpected, failActual);
        }

        [Fact]//Check objects are correctly added/removed from the Hashset.
        public static void ObjectFoundTwiceOnSameDepth()
        {
            //Validate that the 'a' reference remains in the set when hitted somewhere else.
            //a--> b--> a
            //     └--> a  

            Employee angela = new Employee();
            Employee bob = new Employee();

            angela.Subordinates = new List<Employee> { bob };

            bob.Manager = angela;
            bob.Subordinates = new List<Employee> { angela };


            TestSerialize(angela, default, out string outputExpected, out string outputActual, out bool failExpected, out bool failActual);

            Assert.Equal(outputExpected, outputActual);
            Assert.Equal(failExpected, failActual);
        }

        //TODO actually this should write `a` then write preserve `a` then ignore second `a`
        // [ignore] [Preserve]  
        // a    --> a      
        //      |    [ignore]
        //      └-> a

        //Dictionary tests
        [Fact]
        public static void DictionaryLoop()
        {
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["name"] = "root";
            root["self"] = root;

            TestSerialize(root, default, out string outputExpected, out string outputActual, out bool failExpected, out bool failActual);

            Assert.Equal(outputExpected, outputActual);
            Assert.Equal(failExpected, failActual);
        }

        [JsonReferenceHandling(ReferenceHandling.Error)]
        //[Serialization.JsonConverter(typeof(EmployeeConverter))]
        private class Employee2
        {
            public string Name { get; set; }

            [JsonReferenceHandling(ReferenceHandling.Preserve)]
            public Employee2 Manager { get; set; }

            public List<Employee2> Subordinates { get; set; }

            [JsonReferenceHandling(ReferenceHandling.Error)]
            public virtual Employee Prop1 { get; set; }
        }

        //[JsonReferenceHandling(ReferenceHandling.Serialize)]
        private class Manager : Employee2
        {
            public string Title { get; set; }

            //[JsonReferenceHandling(ReferenceHandling.Serialize)]
            public override Employee Prop1 { get => base.Prop1; set => base.Prop1 = value; }
        }

        //[Fact]
        public static string WriteReference()
        {
            var angela = new Manager { Name = "Angela", Title = "The Boss" };
            var bob = new Employee2 { Name = "Bob" };

            angela.Subordinates = new List<Employee2> { bob };
            bob.Manager = angela;

            angela.Prop1 = new Employee { Name = "Carlos" };

            var settings = new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandling = ReferenceHandling.Serialize,
            };

            string json = JsonSerializer.Serialize(angela, settings);

            return json;
        }

        //Structs TODO
        public struct EmployeeStruct
        {
            public string Name { get; set; }
            public List<EmployeeStruct> Subordinates { get; set; }
        }

        //[Fact]
        public static string SerializeStruct()
        {
            var angela = new EmployeeStruct { Name = "Angela" };
            var bob = new EmployeeStruct { Name = "Bob" };

            angela.Subordinates = new List<EmployeeStruct> { bob };
            bob.Subordinates = new List<EmployeeStruct> { angela };

            var settings = new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandling = (ReferenceHandling)(ReferenceHandling.Serialize + 1),


            };

            return JsonSerializer.Serialize(angela, settings);
        }

        [Fact]
        public static void ListOfObjects()
        {
            List<Employee> list = new List<Employee> { new Employee(), new Employee() };

            JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        }

        //utility
        private static void TestSerialize(object obj, ReferenceHandling referenceHandling, out string outputExpected, out string outputActual, out bool failExpected, out bool failActual)
        {
            failExpected = false;
            failActual = false;
            outputExpected = null;
            outputActual = null;

            try
            {
                outputExpected = JsonConvert.SerializeObject(obj, JsonNetSettings(referenceHandling));
            }
            catch (JsonSerializationException)
            {
                failExpected = true;
            }

            try
            {
                outputActual = JsonSerializer.Serialize(obj, SystemTextJsonOptions(referenceHandling));
            }
            catch (JsonTestException)
            {
                failActual = true;
            }
        }

        private static JsonSerializerSettings JsonNetSettings(ReferenceHandling referenceHandling)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            if (referenceHandling == ReferenceHandling.Preserve)
            {
                settings.PreserveReferencesHandling = PreserveReferencesHandling.All;
                settings.ReferenceLoopHandling = ReferenceLoopHandling.Serialize;
            }
            else
            {
                settings.ReferenceLoopHandling = (ReferenceLoopHandling)referenceHandling;
            }

            return settings;
        }

        private static JsonSerializerOptions SystemTextJsonOptions(ReferenceHandling referenceHandling)
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                IgnoreNullValues = true,
                ReferenceHandling = referenceHandling
            };
        }
        //End utility

        //Old tests
        /*
        //[Fact]
        //public static void SerializeArrayInArrayLoop()
        //{
        //    List<object> objectList = new List<object>();
        //    objectList.Add(objectList);
        //    objectList.Add(objectList);

        //    var options = new JsonSerializerOptions
        //    {
        //        WriteIndented = true,
        //        IgnoreNullValues = true,
        //        ReferenceHandling = ReferenceHandling.Preserve
        //    };

        //    string json = JsonSerializer.Serialize(objectList, options);
        //    Console.WriteLine(json);
        //}

        //[Fact]
        //public static void SerializeArrayLoop()
        //{
        //    var Angela = new Employee { Name = "Angela" };
        //    //var Bob = new Employee { Name = "Bob" };

        //    Angela.Subordinates = new List<Employee> { Angela };
        //    //Bob.Manager = Angela;

        //    var options = new JsonSerializerOptions
        //    {
        //        WriteIndented = true,
        //        IgnoreNullValues = true,
        //        ReferenceHandling = ReferenceHandling.Preserve
        //    };

        //    string json = JsonSerializer.Serialize(Angela, options);
        //    Console.WriteLine(json);
        //}

        //[Fact]
        //public static void SerializeObjectWithDuplicateArray() 
        //{
        //    var Angela = new Employee { Name = "Angela" };
        //    var subordinates = new List<Employee> { };

        //    Angela.List1 = subordinates;
        //    Angela.List2 = subordinates;

        //    var options = new JsonSerializerOptions
        //    {
        //        WriteIndented = true,
        //        IgnoreNullValues = true,
        //        ReferenceHandling = ReferenceHandling.Ignore
        //    };

        //    string json = JsonSerializer.Serialize(Angela, options);
        //    Console.WriteLine(json);
        //}

        //[Fact]
        //public static void SerializeObjectLoop()
        //{
        //    var Angela = new Employee { Name = "Angela" };
        //    //var Bob = new Employee { Name = "Bob" };

        //    //Angela.Subordinates = new List<Employee> { Angela };
        //    //Bob.Manager = Angela;
        //    Angela.Manager = Angela;

        //    var options = new JsonSerializerOptions
        //    {
        //        WriteIndented = true,
        //        IgnoreNullValues = true,
        //        ReferenceHandling = ReferenceHandling.Preserve
        //    };

        //    string json = JsonSerializer.Serialize(Angela, options);
        //    Console.WriteLine(json);
        //}

        //[Fact]
        //public static void SerializeReferenceLoop()
        //{
        //    var joe = new Employee { Name = "Joe User" };
        //    var mike = new Employee { Name = "Mike Manager" };
        //    joe.Manager = mike;
        //    mike.Manager = mike;
        //    //mike.Manager.Manager.Manager.Manager = null;

        //    var options = new JsonSerializerOptions { 
        //        WriteIndented = true, 
        //        ReferenceHandling = ReferenceHandling.Ignore 
        //    };

        //    string json = JsonSerializer.Serialize(joe, options);

        //    Console.WriteLine(json);
        //}

        //[Fact]
        //public static void WriteReferenceLoop()
        //{
        //    var joe = new Employee { Name = "Joe User" };
        //    var mike = new Employee { Name = "Mike Manager" };
        //    joe.Manager = mike;
        //    mike.Manager = mike;
        //    //mike.Manager.Manager.Manager.Manager = null;

        //    var options = new JsonSerializerOptions
        //    {
        //        WriteIndented = true,
        //        ReferenceHandling = ReferenceHandling.Ignore
        //    };

        //    var json = JsonSerializer.Serialize(joe, options);

        //    Console.WriteLine(json);
        //}

        //[Fact]
        //public static void WriteReferenceLoopOnList()
        //{
        //    Employee mike = new Employee
        //    {
        //        Name = "Mike - Manager",
        //    };

        //    Employee joe = new Employee
        //    {
        //        Name = "Joe - User",
        //        Manager = mike,
        //    };

        //    mike.Subordinates = new List<Employee>() 
        //    { 
        //        joe 
        //    };

        //    var options = new JsonSerializerOptions
        //    {
        //        WriteIndented = true,
        //        ReferenceHandling = ReferenceHandling.Ignore
        //    };

        //    string json = JsonSerializer.Serialize(joe, options);
        //    Console.WriteLine(json);
        //}
        */
    }
}
