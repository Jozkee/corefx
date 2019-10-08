using Xunit;

namespace System.Text.Json.Tests
{
    public static partial class ReferenceHandlingBenchmark
    {
        private class Employee
        {
            public Employee Manager { get; set; }
        }

        //public static bool IsReferenceLoop { get; set; }
        private static object _object;

        private static void Setup()
        {

            var a = new Employee();
            a.Manager = new Employee();
            a.Manager.Manager = new Employee();
            a.Manager.Manager.Manager = new Employee();
            a.Manager.Manager.Manager.Manager = new Employee();
            a.Manager.Manager.Manager.Manager.Manager = new Employee();
            a.Manager.Manager.Manager.Manager.Manager.Manager = new Employee();
            a.Manager.Manager.Manager.Manager.Manager.Manager.Manager = new Employee();

            Employee z;
            a.Manager.Manager.Manager.Manager.Manager.Manager.Manager.Manager = z = new Employee();

            //Do not loop for now...
            //if (IsReferenceLoop)
            //{
            //    z.Manager = a;
            //}

            _object = a;
        }

        [Fact]
        public static void Benchmark()
        {
            Setup();

            string json = null;
            try
            {
                json = JsonSerializer.Serialize(_object);
            }
            catch (JsonException ex)
            {
                Console.WriteLine(ex.Message);
            }

            //return json;

            Assert.NotNull(json);
        }
    }
}
