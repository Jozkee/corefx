﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Xunit;

namespace System.Text.Json.Tests
{
    public static partial class ReferenceHandlingTests
    {
        private static JsonSerializerOptions _serializeOptionsError = new JsonSerializerOptions { ReferenceHandling = ReferenceHandling.Default };
        private static JsonSerializerOptions _serializeOptionsIgnore = new JsonSerializerOptions { ReferenceHandling = ReferenceHandling.Ignore };
        private static JsonSerializerOptions _serializeOptionsPreserve = new JsonSerializerOptions { ReferenceHandling = ReferenceHandling.Preserve };

        private static JsonSerializerSettings _newtonsoftSerializeOptionsError = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Error};
        private static JsonSerializerSettings _newtonsoftSerializeOptionsIgnore = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
        private static JsonSerializerSettings _newtonsoftSerializeOptionsPreserve = new JsonSerializerSettings { PreserveReferencesHandling = PreserveReferencesHandling.All, ReferenceLoopHandling = ReferenceLoopHandling.Serialize };

        private class Employee
        {
            public static readonly List<string> SubordinatesDefault = new List<string> { "Bob" }; //how can I make these immutables?
            public static readonly Dictionary<string, string> ContactsDefault = new Dictionary<string, string>() { { "Bob", "555-5555" } };

            public string Name { get; set; }
            public Employee Manager { get; set; }
            public Employee Manager2 { get; set; }
            public List<Employee> Subordinates { get; set; }
            public List<Employee> Subordinates2 { get; set; }
            public Dictionary<string, Employee> Contacts { get; set; }
            public Dictionary<string, Employee> Contacts2 { get; set; }

            //Properties with default value.
            public List<string> SubordinatesString { get; set; } = SubordinatesDefault;
            public Dictionary<string, string> ContactsString { get; set; } = ContactsDefault;
        }

        [Fact]
        public static void VerifyDefaultJsonSerializerOptions()
        {
            JsonSerializerOptions opts = new JsonSerializerOptions();

            Assert.Equal(ReferenceHandling.Default, opts.ReferenceHandling);

            Assert.Throws<ArgumentOutOfRangeException>(() => opts.ReferenceHandling = (ReferenceHandling)(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => opts.ReferenceHandling = (ReferenceHandling)(3));
        }

        [Fact]
        public static void ThrowByDefaultOnLoop()
        {
            Employee a = new Employee();
            a.Manager = a;

            JsonException ex = Assert.Throws<JsonException>(() => JsonSerializer.Serialize(a));
            //Assert.Equal("Invalid Reference Loop Detected!.", ex.Message);
            //TODO: Change default throw error msg in order to state that you can deal with loops with any of the other RefHandling options.
            Assert.Contains("A possible object cycle was detected which is not supported.", ex.Message);
        }

        #region Root Object
        [Theory]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        public static void ObjectLoop(ReferenceHandling referenceHandling)
        {
            Employee angela = new Employee();
            angela.Manager = angela;

            string expected = JsonConvert.SerializeObject(angela, JsonNetSettings(referenceHandling));            
            string actual = JsonSerializer.Serialize(angela, SystemTextJsonOptions(referenceHandling));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        public static void ObjectArrayLoop(ReferenceHandling referenceHandling)
        {
            Employee angela = new Employee();
            angela.Subordinates = new List<Employee> { angela };

            string expected = JsonConvert.SerializeObject(angela, JsonNetSettings(referenceHandling));
            string actual = JsonSerializer.Serialize(angela, SystemTextJsonOptions(referenceHandling));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        public static void ObjectDictionaryLoop(ReferenceHandling referenceHandling)
        {
            Employee angela = new Employee();
            angela.Contacts = new Dictionary<string, Employee> { { "555-5555", angela } };

            string expected = JsonConvert.SerializeObject(angela, JsonNetSettings(referenceHandling));
            string actual = JsonSerializer.Serialize(angela, SystemTextJsonOptions(referenceHandling));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void ObjectPreserveDuplicateObjects()
        {
            Employee angela = new Employee();

            angela.Manager = new Employee { Name = "Bob" };
            angela.Manager2 = angela.Manager;



            string expected = JsonConvert.SerializeObject(angela, JsonNetSettings(ReferenceHandling.Preserve));
            string actual = JsonSerializer.Serialize(angela, SystemTextJsonOptions(ReferenceHandling.Preserve));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void ObjectPreserveDuplicateDictionaries()
        {
            Employee angela = new Employee();

            angela.Contacts = new Dictionary<string, Employee> { { "444-4444", new Employee { Name = "Bob" } } };
            angela.Contacts2 = angela.Contacts;

            string expected = JsonConvert.SerializeObject(angela, JsonNetSettings(ReferenceHandling.Preserve));
            string actual = JsonSerializer.Serialize(angela, SystemTextJsonOptions(ReferenceHandling.Preserve));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void ObjectPreserveDuplicateArrays()
        {
            Employee angela = new Employee();

            angela.Subordinates = new List<Employee> { new Employee { Name = "Bob" } };
            angela.Subordinates2 = angela.Subordinates;

            string expected = JsonConvert.SerializeObject(angela, JsonNetSettings(ReferenceHandling.Preserve));
            string actual = JsonSerializer.Serialize(angela, SystemTextJsonOptions(ReferenceHandling.Preserve));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        //Check objects are correctly added/removed from the Hashset.
        public static void ObjectFoundTwiceOnSameDepth(ReferenceHandling handling)
        {
            //Validate that the 'a' reference remains in the set when found somewhere else.
            //a--> b--> a
            //     └--> a  
            Employee angela = new Employee();
            Employee bob = new Employee();

            angela.Subordinates = new List<Employee> { bob };

            bob.Manager = angela;
            bob.Subordinates = new List<Employee> { angela };

            string expected = JsonConvert.SerializeObject(angela, JsonNetSettings(handling));
            string actual = JsonSerializer.Serialize(angela, SystemTextJsonOptions(handling));

            Assert.Equal(expected, actual);
        }
        #endregion

        #region Root Dictionary
        private class MyDictionary : Dictionary<string, MyDictionary> { }

        [Theory]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        public static void DictionaryLoop(ReferenceHandling handling)
        {
            MyDictionary root = new MyDictionary();
            root["Self"] = root;
            root["Other"] = new MyDictionary();

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(handling));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(handling));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        public static void DictionaryObjectLoop(ReferenceHandling referenceHandling)
        {
            Dictionary<string, Employee> root = new Dictionary<string, Employee>();
            root["Angela"] = new Employee() { Name = "Angela", Contacts = root };

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(referenceHandling));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(referenceHandling));

            Assert.Equal(expected, actual);
        }

        private class MyDictionaryArrayValues : Dictionary<string, List<MyDictionaryArrayValues>> { }

        [Theory]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        public static void DictionaryArrayLoop(ReferenceHandling referenceHandling)
        {
            MyDictionaryArrayValues root = new MyDictionaryArrayValues();
            root["ArrayWithSelf"] = new List<MyDictionaryArrayValues> { root };

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(referenceHandling));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(referenceHandling));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void DictionaryPreserveDuplicateDictionaries()
        {
            MyDictionary root = new MyDictionary();
            root["Self1"] = root;
            root["Self2"] = root;

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(ReferenceHandling.Preserve));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(ReferenceHandling.Preserve));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void DictionaryPreserveDuplicateObjects()
        {
            Dictionary<string, Employee> root = new Dictionary<string, Employee>();
            root["Employee1"] = new Employee { Name = "Angela" };
            root["Employee2"] = root["Employee1"];

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(ReferenceHandling.Preserve));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(ReferenceHandling.Preserve));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void DictionaryPreserveDuplicateArrays()
        {
            MyDictionaryArrayValues root = new MyDictionaryArrayValues();
            root["Array1"] = new List<MyDictionaryArrayValues> { root };
            root["Array2"] = root["Array1"];

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(ReferenceHandling.Preserve));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(ReferenceHandling.Preserve));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void DictionaryNTimesUsingIgnore()
        {
            Dictionary<string, Employee> root = new Dictionary<string, Employee>();
            Employee elem = new Employee();
            elem.Contacts = root;
            elem.Contacts2 = root;

            root["angela"] = elem;

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(ReferenceHandling.Ignore));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(ReferenceHandling.Ignore));

            Assert.Equal(expected, actual);
        }
        #endregion

        #region Root Array
        private class MyList : List<MyList> { }

        [Theory]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        public static void ArrayLoop(ReferenceHandling referenceHandling)
        {
            MyList root = new MyList();
            root.Add(root);

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(referenceHandling));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(referenceHandling));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        public static void ArrayObjectLoop(ReferenceHandling referenceHandling)
        {
            List<Employee> root = new List<Employee>();
            root.Add(new Employee() { Name = "Angela", Subordinates = root });

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(referenceHandling));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(referenceHandling));

            Assert.Equal(expected, actual);
        }

        private class MyListDictionaryValues : List<Dictionary<string, MyListDictionaryValues>> { }

        [Theory]
        [InlineData(ReferenceHandling.Ignore)]
        [InlineData(ReferenceHandling.Preserve)]
        public static void ArrayDictionaryLoop(ReferenceHandling referenceHandling)
        {
            MyListDictionaryValues root = new MyListDictionaryValues();
            root.Add(new Dictionary<string, MyListDictionaryValues> { { "Root", root } });

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(referenceHandling));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(referenceHandling));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void ArrayPreserveDuplicateArrays()
        {
            MyList root = new MyList();
            root.Add(root);
            root.Add(root);
            root.Add(root);

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(ReferenceHandling.Preserve));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(ReferenceHandling.Preserve));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void ArrayPreserveDuplicateObjects()
        {
            List<Employee> root = new List<Employee>();
            root.Add(new Employee { Name = "Angela" });
            root.Add(root[0]);

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(ReferenceHandling.Preserve));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(ReferenceHandling.Preserve));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void ArrayPreserveDuplicateDictionaries()
        {
            MyListDictionaryValues root = new MyListDictionaryValues();
            root.Add(new Dictionary<string, MyListDictionaryValues>());
            root.Add(root[0]);

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(ReferenceHandling.Preserve));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(ReferenceHandling.Preserve));

            Assert.Equal(expected, actual);
        }

        [Fact]//Check objects are correctly added/removed from the Hashset.
        public static void ObjectUnevenTimesUsingIgnore()
        {
            List<Employee> employees = new List<Employee>();

            Employee angela = new Employee();
            Employee bob = new Employee();

            bob.Manager = angela;
            angela.Manager = angela;

            employees.Add(bob);
            employees.Add(bob);
            employees.Add(bob);

            string expected = JsonConvert.SerializeObject(employees, JsonNetSettings(ReferenceHandling.Ignore));
            string actual = JsonSerializer.Serialize(employees, SystemTextJsonOptions(ReferenceHandling.Ignore));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void ArrayNTimesUsingIgnore()
        {
            List<Employee> root = new List<Employee>();
            Employee elem = new Employee();
            elem.Subordinates = root;
            elem.Subordinates2 = root;

            root.Add(elem);

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(ReferenceHandling.Ignore));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(ReferenceHandling.Ignore));

            Assert.Equal(expected, actual);
        }
        #endregion

        #region struct tests
        private struct EmployeeStruct
        {
            public string Name { get; set; }
        }

        [Fact]
        public static void PreserveStruct()
        {
            EmployeeStruct elem = new EmployeeStruct { Name = "Angela" };
            List<EmployeeStruct> root = new List<EmployeeStruct> { elem, elem };

            string expected = JsonConvert.SerializeObject(root, JsonNetSettings(ReferenceHandling.Preserve));
            string actual = JsonSerializer.Serialize(root, SystemTextJsonOptions(ReferenceHandling.Preserve));

            Assert.Equal(expected, actual);
        }
        #endregion struct tests


        //utility
        private static JsonSerializerSettings JsonNetSettings(ReferenceHandling referenceHandling)
        {
            switch(referenceHandling){
                case ReferenceHandling.Default:
                    return _newtonsoftSerializeOptionsError;
                case ReferenceHandling.Ignore:
                    return _newtonsoftSerializeOptionsIgnore;
                case ReferenceHandling.Preserve:
                    return _newtonsoftSerializeOptionsPreserve;
            }

            return _newtonsoftSerializeOptionsError;
        }

        private static JsonSerializerOptions SystemTextJsonOptions(ReferenceHandling referenceHandling)
        {
            switch (referenceHandling)
            {
                case ReferenceHandling.Default:
                    return _serializeOptionsError;
                case ReferenceHandling.Ignore:
                    return _serializeOptionsIgnore;
                case ReferenceHandling.Preserve:
                    return _serializeOptionsPreserve;
            }

            return _serializeOptionsError;
        }
        //End utility

    }
}
