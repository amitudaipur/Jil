﻿using Jil;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JilTests
{
    [TestClass]
    public class DynamicSerializationTests
    {
        [TestMethod]
        public void ToStringJSON()
        {
            {
                var dyn = JSON.DeserializeDynamic("{\"Hello\":1}");
                var res = dyn.ToString();
                var shouldMatch = JSON.Serialize(new { Hello = 1 }, Options.ISO8601PrettyPrint);
                Assert.AreEqual(shouldMatch, res);
            }

            {
                var dyn1 = JSON.DeserializeDynamic(long.MaxValue.ToString());
                var dyn2 = JSON.DeserializeDynamic(ulong.MaxValue.ToString());
                var dyn3 = JSON.DeserializeDynamic(long.MinValue.ToString());
                var res1 = dyn1.ToString();
                var res2 = dyn2.ToString();
                var res3 = dyn3.ToString();
                Assert.AreEqual(long.MaxValue.ToString(), res1);
                Assert.AreEqual(ulong.MaxValue.ToString(), res2);
                Assert.AreEqual(long.MinValue.ToString(), res3);
            }

            {
                var dyn = JSON.DeserializeDynamic("1.23456");
                var res = dyn.ToString();
                Assert.AreEqual("1.23456", res);
            }

            {
                var dyn1 = JSON.DeserializeDynamic("true");
                var dyn2 = JSON.DeserializeDynamic("false");
                var res1 = dyn1.ToString();
                var res2 = dyn2.ToString();
                Assert.AreEqual("true", res1);
                Assert.AreEqual("false", res2);
            }

            {
                var now = DateTime.UtcNow;
                var str = JSON.Serialize(now, Options.ISO8601);
                var dyn = JSON.DeserializeDynamic(str, Options.ISO8601);
                var res = dyn.ToString();
                Assert.AreEqual(str, res);
            }

            {
                var g = Guid.NewGuid();
                var str = JSON.Serialize(g);
                var dyn = JSON.DeserializeDynamic(str);
                var res = dyn.ToString();
                Assert.AreEqual(str, res);
            }

            {
                var dyn = JSON.DeserializeDynamic("\"how are you today?\"");
                var str = dyn.ToString();
                Assert.AreEqual("\"how are you today?\"", str);
            }

            {
                var dyn1 = JSON.DeserializeDynamic("[1,2,3]");
                var dyn2 = JSON.DeserializeDynamic("[]");
                var dyn3 = JSON.DeserializeDynamic("[1, \"hello\", {}, 456]");
                var res1 = dyn1.ToString();
                var res2 = dyn2.ToString();
                var res3 = dyn3.ToString();
                var shouldMatch1 = JSON.Serialize(new[] { 1, 2, 3 }, Options.ISO8601PrettyPrint);
                var shouldMatch2 = JSON.Serialize(new object[0], Options.ISO8601PrettyPrint);
                var shouldMatch3 =
                    "[" +
                    JSON.Serialize(1, Options.ISO8601PrettyPrint) +
                    ", " +
                    JSON.Serialize("hello", Options.ISO8601PrettyPrint) +
                    ", " +
                    JSON.Serialize(new { }, Options.ISO8601PrettyPrint) +
                    ", " +
                    JSON.Serialize(456, Options.ISO8601PrettyPrint) +
                    "]";
                Assert.AreEqual(shouldMatch1, res1);
                Assert.AreEqual(shouldMatch2, res2);
                Assert.AreEqual(shouldMatch3, res3);
            }
        }

        [TestMethod]
        public void HeterogenousCollection()
        {
            using (var str = new StringWriter())
            {
                var dict = (dynamic)new ExpandoObject();
                dict.Fizz = "Buzz";
                var arr = new object[] { 123, "hello", new { Foo = "bar" }, dict };

                JSON.SerializeDynamic(arr, str);
                var res = str.ToString();

                Assert.AreEqual("[123,\"hello\",{\"Foo\":\"bar\"},{\"Fizz\":\"Buzz\"}]", res);
            }
        }

        [TestMethod]
        public void Objects()
        {
            using (var str = new StringWriter())
            {
                var dict = (dynamic)new ExpandoObject();
                dict.Foo = 123;
                dict.Bar = "hello";
                JSON.SerializeDynamic(dict, str);
                var res = str.ToString();
                Assert.AreEqual("{\"Foo\":123,\"Bar\":\"hello\"}", res);
            }
        }

        [TestMethod]
        public void Simple()
        {
            using (var str = new StringWriter())
            {
                JSON.SerializeDynamic(123, str);
                var res = str.ToString();
                Assert.AreEqual("123", res);
            }

            using (var str = new StringWriter())
            {
                JSON.SerializeDynamic("hello", str);
                var res = str.ToString();
                Assert.AreEqual("\"hello\"", res);
            }

            using (var str = new StringWriter())
            {
                JSON.SerializeDynamic(null, str);
                var res = str.ToString();
                Assert.AreEqual("null", res);
            }

            using (var str = new StringWriter())
            {
                var now = DateTime.UtcNow;
                JSON.SerializeDynamic(now, str);
                var res = str.ToString();
                var dt = JSON.Deserialize<DateTime>(res);
                Assert.IsTrue((now - dt).Duration() < TimeSpan.FromMilliseconds(1));
            }
        }

        #region PersonElasticMigration methods and types

        static dynamic Describe(Type t, string memberName)
        {
            if (Nullable.GetUnderlyingType(t) != null)
            {
                return Describe(Nullable.GetUnderlyingType(t), memberName);
            }

            if (t == typeof(string) || t == typeof(Guid))
            {
                return new
                {
                    type = "string",
                    index = memberName == "Id" ? "not_analyzed" : "no"
                };
            }

            if (t == typeof(int))
            {
                return new
                {
                    type = "integer",
                    index = "no"
                };
            }

            if (t == typeof(long))
            {
                return new
                {
                    type = "long",
                    index = "no"
                };
            }

            if (t == typeof(float))
            {
                return new
                {
                    type = "float",
                    index = "no"
                };
            }

            if (t == typeof(DateTime))
            {
                return new
                {
                    type = "date",
                    format = "dateOptionalTime",
                    index = "no"
                };
            }

            if (t.IsValueType) throw new Exception("Unexpected valuetype: " + t.Name);

            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                return Describe(t.GetGenericArguments()[0], "--array--");
            }

            var ret = new Dictionary<string, dynamic>();
            var properties = new Dictionary<string, dynamic>();
            foreach (var prop in t.GetProperties())
            {
                var propName = prop.Name;
                var propType = prop.PropertyType;
                properties[propName] = Describe(propType, propName);
            }

            ret["properties"] = properties;
            if (memberName == "--root--")
            {
                ret["dynamic"] = "strict";
                ret["_all"] = new { enabled = false };
            }
            else
            {
                // can't specify type on the root; I guess object is implicit?
                ret["type"] = "object";
            }

            return ret;
        }

        public class Person
        {
            public Guid Id { get; set; }
            public MostRecentLocation MostRecentLocation { get; set; }
            public List<Location> Locations { get; set; }
            public List<InterestingTag> InterestingTags { get; set; }
            public List<PersonIdentifier> Identifiers { get; set; }
            public List<InterestingSite> InterestingSites { get; set; }
            public List<TagView> TagViews { get; set; }
            public List<DeveloperKind> DeveloperKinds { get; set; }
            public List<Demographic> Demographics { get; set; }
            public List<Education> Educations { get; set; }
            public List<Industry> Industries { get; set; }
            public List<Language> Languages { get; set; }
            public List<WorkingHour> WorkingHours { get; set; }
            public DateTime? LastSeen { get; set; }
            public List<Merge> Merges { get; set; }
        }

        public class MostRecentLocation
        {
            public float Latitude { get; set; }
            public float Longitude { get; set; }
            public DateTime LastSeenDate { get; set; }
        }

        public class Location
        {
            public class LocationGeoPoint
            {
                public float Latitude { get; set; }
                public float Longitude { get; set; }
            }

            public string CountryCode { get; set; }

            public int LocType { get; set; }
            public DateTime OnDate { get; set; }
            public int SeenCount { get; set; }
            public LocationGeoPoint GeoPoint { get; set; }

            public string Name { get; set; }
        }

        public class InterestingTag
        {
            public int SiteId { get; set; }
            public int TagId { get; set; }
            public float Confidence { get; set; }
        }

        public class PersonIdentifier
        {
            public string Id { get; set; }
            public int IdType { get; set; }
        }

        public class InterestingSite
        {
            public int SiteId { get; set; }
            public float InterestLevel { get; set; }
        }

        public class TagView
        {
            public int SiteId { get; set; }
            public int TagId { get; set; }
            public long TimesViewed { get; set; }
        }

        public class DeveloperKind
        {
            public int DevType { get; set; }
            public float RelativeScore { get; set; }
        }

        public class Demographic
        {
            [JilDirective(Ignore = true)]
            public Guid PersonId { get; set; }

            public int DeviceTypeId { get; set; }
            public int BrowserId { get; set; }
            public int OsId { get; set; }

            public DateTime OnDate { get; set; }
            public int SeenCount { get; set; }
        }

        public class Education
        {
            public string Tld { get; set; }
            public string Name { get; set; }

            public DateTime OnDate { get; set; }
            public int SeenCount { get; set; }

            public int SourceId { get; set; }
        }

        public class Industry
        {
            public Guid PersonId { get; set; }
            public int IndustryId { get; set; }
            public DateTime OnDate { get; set; }
            public int SeenCount { get; set; }
        }

        public class Language
        {
            public string LanguageCode { get; set; }

            public int LangSource { get; set; }
            public DateTime OnDate { get; set; }
            public int SeenCount { get; set; }
        }

        public class WorkingHour
        {
            public int Hour { get; set; }
            public long Count { get; set; }
        }

        public class Merge
        {
            public Guid DestroyedPersonId { get; set; }
            public DateTime CreationDate { get; set; }
        }

        #endregion

        [TestMethod]
        public void PersonElasticMigration()
        {
            var personDescribed = Describe(typeof(Person), "--root--");
            var json = JSON.SerializeDynamic(personDescribed);
            Assert.AreEqual("{\"properties\":{\"Id\":{\"index\":\"not_analyzed\",\"type\":\"string\"},\"MostRecentLocation\":{\"properties\":{\"Latitude\":{\"index\":\"no\",\"type\":\"float\"},\"Longitude\":{\"index\":\"no\",\"type\":\"float\"},\"LastSeenDate\":{\"index\":\"no\",\"format\":\"dateOptionalTime\",\"type\":\"date\"}},\"type\":\"object\"},\"Locations\":{\"properties\":{\"CountryCode\":{\"index\":\"no\",\"type\":\"string\"},\"LocType\":{\"index\":\"no\",\"type\":\"integer\"},\"OnDate\":{\"index\":\"no\",\"format\":\"dateOptionalTime\",\"type\":\"date\"},\"SeenCount\":{\"index\":\"no\",\"type\":\"integer\"},\"GeoPoint\":{\"properties\":{\"Latitude\":{\"index\":\"no\",\"type\":\"float\"},\"Longitude\":{\"index\":\"no\",\"type\":\"float\"}},\"type\":\"object\"},\"Name\":{\"index\":\"no\",\"type\":\"string\"}},\"type\":\"object\"},\"InterestingTags\":{\"properties\":{\"SiteId\":{\"index\":\"no\",\"type\":\"integer\"},\"TagId\":{\"index\":\"no\",\"type\":\"integer\"},\"Confidence\":{\"index\":\"no\",\"type\":\"float\"}},\"type\":\"object\"},\"Identifiers\":{\"properties\":{\"Id\":{\"index\":\"not_analyzed\",\"type\":\"string\"},\"IdType\":{\"index\":\"no\",\"type\":\"integer\"}},\"type\":\"object\"},\"InterestingSites\":{\"properties\":{\"SiteId\":{\"index\":\"no\",\"type\":\"integer\"},\"InterestLevel\":{\"index\":\"no\",\"type\":\"float\"}},\"type\":\"object\"},\"TagViews\":{\"properties\":{\"SiteId\":{\"index\":\"no\",\"type\":\"integer\"},\"TagId\":{\"index\":\"no\",\"type\":\"integer\"},\"TimesViewed\":{\"index\":\"no\",\"type\":\"long\"}},\"type\":\"object\"},\"DeveloperKinds\":{\"properties\":{\"DevType\":{\"index\":\"no\",\"type\":\"integer\"},\"RelativeScore\":{\"index\":\"no\",\"type\":\"float\"}},\"type\":\"object\"},\"Demographics\":{\"properties\":{\"PersonId\":{\"index\":\"no\",\"type\":\"string\"},\"DeviceTypeId\":{\"index\":\"no\",\"type\":\"integer\"},\"BrowserId\":{\"index\":\"no\",\"type\":\"integer\"},\"OsId\":{\"index\":\"no\",\"type\":\"integer\"},\"OnDate\":{\"index\":\"no\",\"format\":\"dateOptionalTime\",\"type\":\"date\"},\"SeenCount\":{\"index\":\"no\",\"type\":\"integer\"}},\"type\":\"object\"},\"Educations\":{\"properties\":{\"Tld\":{\"index\":\"no\",\"type\":\"string\"},\"Name\":{\"index\":\"no\",\"type\":\"string\"},\"OnDate\":{\"index\":\"no\",\"format\":\"dateOptionalTime\",\"type\":\"date\"},\"SeenCount\":{\"index\":\"no\",\"type\":\"integer\"},\"SourceId\":{\"index\":\"no\",\"type\":\"integer\"}},\"type\":\"object\"},\"Industries\":{\"properties\":{\"PersonId\":{\"index\":\"no\",\"type\":\"string\"},\"IndustryId\":{\"index\":\"no\",\"type\":\"integer\"},\"OnDate\":{\"index\":\"no\",\"format\":\"dateOptionalTime\",\"type\":\"date\"},\"SeenCount\":{\"index\":\"no\",\"type\":\"integer\"}},\"type\":\"object\"},\"Languages\":{\"properties\":{\"LanguageCode\":{\"index\":\"no\",\"type\":\"string\"},\"LangSource\":{\"index\":\"no\",\"type\":\"integer\"},\"OnDate\":{\"index\":\"no\",\"format\":\"dateOptionalTime\",\"type\":\"date\"},\"SeenCount\":{\"index\":\"no\",\"type\":\"integer\"}},\"type\":\"object\"},\"WorkingHours\":{\"properties\":{\"Hour\":{\"index\":\"no\",\"type\":\"integer\"},\"Count\":{\"index\":\"no\",\"type\":\"long\"}},\"type\":\"object\"},\"LastSeen\":{\"index\":\"no\",\"format\":\"dateOptionalTime\",\"type\":\"date\"},\"Merges\":{\"properties\":{\"DestroyedPersonId\":{\"index\":\"no\",\"type\":\"string\"},\"CreationDate\":{\"index\":\"no\",\"format\":\"dateOptionalTime\",\"type\":\"date\"}},\"type\":\"object\"}},\"dynamic\":\"strict\",\"_all\":{\"enabled\":false}}", json);
        }

        class _DynamicObject : DynamicObject
        {
            private object ConvertableTo;

            public _DynamicObject(object convertableTo)
            {
                ConvertableTo = convertableTo;
            }

            public override bool TryConvert(ConvertBinder binder, out object result)
            {
                if (binder.ReturnType.IsAssignableFrom(ConvertableTo.GetType()))
                {
                    result = ConvertableTo;
                    return true;
                }

                result = null;
                return false;
            }
        }

        [TestMethod]
        public void DynamicObject()
        {
            Assert.AreEqual("true", JSON.SerializeDynamic(new _DynamicObject(true)));
            Assert.AreEqual("false", JSON.SerializeDynamic(new _DynamicObject(false)));
            Assert.AreEqual("123", JSON.SerializeDynamic(new _DynamicObject(123UL)));
            Assert.AreEqual("123", JSON.SerializeDynamic(new _DynamicObject(123L)));
            Assert.AreEqual("-123", JSON.SerializeDynamic(new _DynamicObject(-123L)));
            Assert.AreEqual("3.14159", JSON.SerializeDynamic(new _DynamicObject(3.14159)));
            Assert.AreEqual("3.14159", JSON.SerializeDynamic(new _DynamicObject(3.14159f)).Substring(0, 7));
            Assert.AreEqual("3.14159", JSON.SerializeDynamic(new _DynamicObject(3.14159m)));
            Assert.AreEqual("\"hello world\"", JSON.SerializeDynamic(new _DynamicObject("hello world")));
            Assert.AreEqual("\"c\"", JSON.SerializeDynamic(new _DynamicObject('c')));

            var now = DateTime.UtcNow;
            Assert.AreEqual(JSON.Serialize(now), JSON.SerializeDynamic(new _DynamicObject(now)));

            var nowOffset = DateTimeOffset.UtcNow;
            Assert.AreEqual(JSON.Serialize(nowOffset), JSON.SerializeDynamic(new _DynamicObject(nowOffset)));

            var g = Guid.NewGuid();
            Assert.AreEqual("\"" + g + "\"", JSON.SerializeDynamic(new _DynamicObject(g)));

            Assert.AreEqual("[1,2,3]", JSON.SerializeDynamic(new _DynamicObject(new[] { 1, 2, 3 })));
        }

        [TestMethod]
        public void ExpandoObject()
        {
            dynamic dyn = new ExpandoObject();
            dyn.A = "B";
            dyn.C = 123;
            dyn.D = new { Foo = "Bar" };
            dyn.E = new[] { 1, 2, 3, 4, 5, 6 };
            dyn.F = new ExpandoObject();
            dyn.F.A = "nope";

            var res = JSON.SerializeDynamic(dyn);
            Assert.AreEqual("{\"A\":\"B\",\"C\":123,\"D\":{\"Foo\":\"Bar\"},\"E\":[1,2,3,4,5,6],\"F\":{\"A\":\"nope\"}}", res);
        }

        [TestMethod]
        public void RecursiveObjects()
        {
            {
                var res = JSON.SerializeDynamic(new { foo = (object)new { baz1 = "1" }, bar = (object)new { baz2 = "2" } }, Options.ISO8601PrettyPrintExcludeNulls);
                Assert.AreEqual("{\n \"foo\": {\n  \"baz1\": \"1\"\n },\n \"bar\": {\n  \"baz2\": \"2\"\n }\n}", res);
            }

            {
                var res = JSON.SerializeDynamic(new { foo = new object[] { new { baz1 = "1" } }, bar = (object)new { baz2 = "2" } }, Options.ISO8601PrettyPrintExcludeNulls);
                Assert.AreEqual("{\n \"foo\": [{\n  \"baz1\": \"1\"\n }],\n \"bar\": {\n  \"baz2\": \"2\"\n }\n}", res);
            }

            {
                var res = JSON.SerializeDynamic(new { foo = new List<object> { new { barz = "1" } }, bar = (object)new { baz2 = "2" } }, Options.ISO8601PrettyPrintExcludeNulls);
                Assert.AreEqual("{\n \"foo\": [{\n  \"barz\": \"1\"\n }],\n \"bar\": {\n  \"baz2\": \"2\"\n }\n}", res);
            }
        }

        [TestMethod]
        public void Issue87()
        {
            string json = "{\"datalist\":[{\"timestamp\":1413131613787,\"roomSchedule\":{\"roomName\":\"21\",\"timestamp\":1413131608000,\"schedule\":[{\"actualStart\":1413115680000,\"canceled\":false,\"duration\":35100000,\"precautions\":false,\"surgeon\":\"some, guy\",\"anonId\":\"666\",\"isFirst\":true,\"service\":\"svc\",\"hideName\":false,\"id\":\"1039666\",\"state\":\"intra\",\"location\":\"Or 21\",\"actualEnd\":1413150780000,\"plannedStart\":1413114300000,\"status\":\"surgStart\",\"started\":true,\"ssn\":\"123-45-6789\",\"isCurrent\":true,\"fullName\":\"WW, FF\",\"room\":\"21\",\"name\":\"WW\",\"dob\":\"01/01/1801\",\"plannedEnd\":1413149400000,\"scheduledStart\":1413114300000,\"mrn\":\"0000004\",\"procedure\":\"ZZ\",\"turnover\":1800000}]},\"unitId\":\"AA\"}]}";

            dynamic obj = JSON.DeserializeDynamic(json);

            var watch = new Stopwatch();
            watch.Start();
            string ser = JSON.SerializeDynamic(obj);
            watch.Stop();
            // 200ms is kind of arbitrary, but it was > 1000 before this Issue was fixed
            Assert.IsTrue(watch.ElapsedMilliseconds < 200, "Took too long to SerializeDynamic, [" + watch.ElapsedMilliseconds + "ms]");
            // technically this isn't guaranteed to be an exact match, but for a test case?  Good enough
            Assert.AreEqual(json, ser);
        }

        [TestMethod]
        public void NullArrayElements()
        {
            using (var str = new StringWriter())
            {
                var obj =
                    new
                    {
                        ids = new string[] { null, "US", "HI" }
                    };
                JSON.SerializeDynamic(obj, str, new Options(excludeNulls: true));

                var res = str.ToString();
                Assert.AreEqual("{\"ids\":[null,\"US\",\"HI\"]}", res);
            }
        }

        [TestMethod]
        public void ExcludingNulls()
        {
            // to stream tests
            {
                using (var str = new StringWriter())
                {
                    JSON.SerializeDynamic(null, str, Options.Default);
                    var res = str.ToString();

                    Assert.AreEqual("null", res);
                }


                using (var str = new StringWriter())
                {
                    JSON.SerializeDynamic(null, str, Options.ExcludeNulls);
                    var res = str.ToString();

                    // it's not a member, it should be written
                    Assert.AreEqual("null", res);
                }

                using (var str = new StringWriter())
                {
                    JSON.SerializeDynamic(new[] { null, "hello", "world" }, str, Options.Default);
                    var res = str.ToString();

                    Assert.AreEqual("[null,\"hello\",\"world\"]", res);
                }

                using (var str = new StringWriter())
                {
                    JSON.SerializeDynamic(new[] { null, "hello", "world" }, str, Options.ExcludeNulls);
                    var res = str.ToString();

                    // it's not a member, it should be written
                    Assert.AreEqual("[null,\"hello\",\"world\"]", res);
                }

                using (var str = new StringWriter())
                {
                    var data = new Dictionary<string, int?>();
                    data["hello"] = 123;
                    data["world"] = null;

                    JSON.SerializeDynamic(data, str, Options.Default);
                    var res = str.ToString();

                    Assert.AreEqual("{\"hello\":123,\"world\":null}", res);
                }

                using (var str = new StringWriter())
                {
                    var data = new Dictionary<string, int?>();
                    data["hello"] = 123;
                    data["world"] = null;

                    JSON.SerializeDynamic(data, str, Options.ExcludeNulls);
                    var res = str.ToString();

                    Assert.AreEqual("{\"hello\":123}", res);
                }

                using (var str = new StringWriter())
                {
                    var data =
                        new
                        {
                            hello = 123,
                            world = default(object)
                        };

                    JSON.SerializeDynamic(data, str, Options.Default);
                    var res = str.ToString();

                    Assert.AreEqual("{\"hello\":123,\"world\":null}", res);
                }

                using (var str = new StringWriter())
                {
                    var data =
                        new
                        {
                            hello = 123,
                            world = default(object)
                        };

                    JSON.SerializeDynamic(data, str, Options.ExcludeNulls);
                    var res = str.ToString();

                    Assert.AreEqual("{\"hello\":123}", res);
                }
            }

            // to string tests
            {
                {
                    var res = JSON.SerializeDynamic(null, Options.Default);

                    Assert.AreEqual("null", res);
                }


                {
                    var res = JSON.SerializeDynamic(null, Options.ExcludeNulls);

                    // it's not a member, it should be written
                    Assert.AreEqual("null", res);
                }

                {
                    var res = JSON.SerializeDynamic(new[] { null, "hello", "world" }, Options.Default);

                    Assert.AreEqual("[null,\"hello\",\"world\"]", res);
                }

                {
                    var res = JSON.SerializeDynamic(new[] { null, "hello", "world" }, Options.ExcludeNulls);

                    // it's not a member, it should be written
                    Assert.AreEqual("[null,\"hello\",\"world\"]", res);
                }

                {
                    var data = new Dictionary<string, int?>();
                    data["hello"] = 123;
                    data["world"] = null;

                    var res = JSON.SerializeDynamic(data, Options.Default);

                    Assert.AreEqual("{\"hello\":123,\"world\":null}", res);
                }

                {
                    var data = new Dictionary<string, int?>();
                    data["hello"] = 123;
                    data["world"] = null;

                    var res = JSON.SerializeDynamic(data, Options.ExcludeNulls);

                    Assert.AreEqual("{\"hello\":123}", res);
                }

                {
                    var data =
                        new
                        {
                            hello = 123,
                            world = default(object)
                        };

                    var res = JSON.SerializeDynamic(data, Options.Default);

                    Assert.AreEqual("{\"hello\":123,\"world\":null}", res);
                }

                {
                    var data =
                        new
                        {
                            hello = 123,
                            world = default(object)
                        };

                    var res = JSON.SerializeDynamic(data, Options.ExcludeNulls);

                    Assert.AreEqual("{\"hello\":123}", res);
                }
            }
        }

        public class _ElasticExampleFailure
        {
            private List<object> _must = new List<object>();
            private List<object> _must_not = new List<object>();
            private List<object> _should = new List<object>();

            public object[] must { get { return GetClean(_must); } }
            public object[] must_not { get { return GetClean(_must_not); } }
            public object[] should { get { return GetClean(_should); } }
            public int? minimum_number_should_match { get; set; }
            public float? boost { get; set; }
            public bool? _cache { get; set; }

            public void Append(_ElasticExampleFailure toAppend)
            {
                if (toAppend.must != null) this._must.AddRange(toAppend.must);
                if (toAppend.must_not != null) this._must_not.AddRange(toAppend.must_not);
                if (toAppend.should != null) this._should.AddRange(toAppend.should);
            }

            private object[] GetClean(List<object> list)
            {
                return list.Any() ? list.ToArray() : null;
            }

            public void AddMust(object query)
            {
                if (query != null)
                {
                    _must.Add(query);
                }
            }

            public void AddMustNot(object query)
            {
                if (query != null)
                {
                    _must_not.Add(query);
                }
            }

            public void AddShould(object query)
            {
                if (query != null)
                {
                    _should.Add(query);
                }
            }

            public bool HasTerms()
            {
                return _must.Any() || _must_not.Any() || _should.Any();
            }

            public static _ElasticExampleFailure BuildSkillsQuery(string queryString)
            {
                var skillsBoolQuery = new _ElasticExampleFailure
                {
                    minimum_number_should_match = 1
                };

                // we determined that one query is a requirement here, and no more becuase
                // otherwise it will try to run the entire boolean query on just the fields selected
                // when we add advanced search so people can specify specific fields for specific text
                // we will still need this inner bool query
                var fieldsForQueryString = new List<string>
                {
                    "personalStatement",
                    "yearsOfExperienceTags^1.5",
                    "stackExchangeAnswersTags^0.1",
                    "name",
                    "likeTags",
                    "stackOverflowUserName",
                    "projects.projectName",
                    "projects.projectTags",
                    "projects.projectDescription",
                    "experience.experienceJobTitle",
                    "experience.experienceEmployerName",
                    "experience.experienceTags",
                    "experience.experienceResponsibilities",
                    "education.educationInstitution",
                    "education.educationTags",
                    "education.educationDegreeName",
                    "education.educationAchievements",
                };

                skillsBoolQuery.AddShould(new
                {
                    query_string = new
                    {
                        query = queryString,
                        default_operator = "AND",
                        fields = fieldsForQueryString.ToArray(),
                        use_dis_max = true,
                    }
                });
                return skillsBoolQuery;
            }
        }

        [TestMethod]
        public void ElasticExampleFailure()
        {
            var mainQuery = new _ElasticExampleFailure();
            var filterQuery = new _ElasticExampleFailure() { _cache = true };
            filterQuery.AddMustNot(new { term = new { blocking = 1234 }, });
            mainQuery.AddMust(_ElasticExampleFailure.BuildSkillsQuery("Dean Ward"));

            object filteredQuery = 
                new
                {
                    query = new { @bool = mainQuery },
                    filter = new { @bool = filterQuery }
                };

            object queryObject = 
                new
                {
                    from = 0,
                    size = 30,
                    query = new
                    {
                        filtered = filteredQuery
                    },
                };

            var options = new Options(prettyPrint: true);
            var res = JSON.SerializeDynamic(queryObject, options);

            Assert.AreEqual("{\n \"size\": 30,\n \"from\": 0,\n \"query\": {\n  \"filtered\": {\n   \"query\": {\n    \"bool\": {\n     \"must\": [{\n      \"must\": null,\n      \"must_not\": null,\n      \"should\": [{\n       \"query_string\": {\n        \"use_dis_max\": true,\n        \"fields\": [\"personalStatement\", \"yearsOfExperienceTags^1.5\", \"stackExchangeAnswersTags^0.1\", \"name\", \"likeTags\", \"stackOverflowUserName\", \"projects.projectName\", \"projects.projectTags\", \"projects.projectDescription\", \"experience.experienceJobTitle\", \"experience.experienceEmployerName\", \"experience.experienceTags\", \"experience.experienceResponsibilities\", \"education.educationInstitution\", \"education.educationTags\", \"education.educationDegreeName\", \"education.educationAchievements\"],\n        \"default_operator\": \"AND\",\n        \"query\": \"Dean Ward\"\n       }\n      }],\n      \"_cache\": null,\n      \"boost\": null,\n      \"minimum_number_should_match\": 1\n     }],\n     \"must_not\": null,\n     \"should\": null,\n     \"_cache\": null,\n     \"boost\": null,\n     \"minimum_number_should_match\": null\n    }\n   },\n   \"filter\": {\n    \"bool\": {\n     \"must\": null,\n     \"must_not\": [{\n      \"term\": {\n       \"blocking\": 1234\n      }\n     }],\n     \"should\": null,\n     \"_cache\": true,\n     \"boost\": null,\n     \"minimum_number_should_match\": null\n    }\n   }\n  }\n }\n}", res);
        }

        abstract class _RecursiveDynamic_Abstract
        {
            public _RecursiveDynamic_Abstract[] SubMembers { get; set; }
            public object[] SubMembersAsObjects { get { return SubMembers == null ? null : SubMembers.Cast<object>().ToArray(); } }
            public int A { get; set; }
        }

        class _RecursiveDynamic : _RecursiveDynamic_Abstract
        {
            public double B { get; set; }
        }

        [TestMethod]
        public void RecursiveDynamic()
        {
            object[] foo = new object[]
                {
                    new
                    {
                        Item = (object)new _RecursiveDynamic
                        {
                            A = 999,
                            B = -999,
                            SubMembers = new[] { new _RecursiveDynamic { A = 1, B = 2.0, SubMembers = new[] { new _RecursiveDynamic { A = 5, B = 6.6 } } }, new _RecursiveDynamic { A = 3, B = 4 } }
                        }
                    },
                    new
                    {
                        Item = (object)new _RecursiveDynamic
                        {
                            A = 999,
                            B = -999,
                            SubMembers = new[] { new _RecursiveDynamic { A = 1, B = 2.0, SubMembers = new[] { new _RecursiveDynamic { A = 5, B = 6.6 } } }, new _RecursiveDynamic { A = 3, B = 4 } }
                        }
                    }
                };

            var res = JSON.SerializeDynamic(foo, Options.PrettyPrintExcludeNullsIncludeInherited);

            Assert.AreEqual("[{\n \"Item\": {\n  \"A\": 999,\n  \"B\": -999,\n  \"SubMembers\": [{\n   \"A\": 1,\n   \"B\": 2,\n   \"SubMembers\": [{\n    \"A\": 5,\n    \"B\": 6.6\n   }],\n   \"SubMembersAsObjects\": [{\n    \"A\": 5,\n    \"B\": 6.6\n   }]\n  }, {\n   \"A\": 3,\n   \"B\": 4\n  }],\n  \"SubMembersAsObjects\": [{\n   \"A\": 1,\n   \"B\": 2,\n   \"SubMembers\": [{\n    \"A\": 5,\n    \"B\": 6.6\n   }],\n   \"SubMembersAsObjects\": [{\n    \"A\": 5,\n    \"B\": 6.6\n   }]\n  }, {\n   \"A\": 3,\n   \"B\": 4\n  }]\n }\n}, {\n \"Item\": {\n  \"A\": 999,\n  \"B\": -999,\n  \"SubMembers\": [{\n   \"A\": 1,\n   \"B\": 2,\n   \"SubMembers\": [{\n    \"A\": 5,\n    \"B\": 6.6\n   }],\n   \"SubMembersAsObjects\": [{\n    \"A\": 5,\n    \"B\": 6.6\n   }]\n  }, {\n   \"A\": 3,\n   \"B\": 4\n  }],\n  \"SubMembersAsObjects\": [{\n   \"A\": 1,\n   \"B\": 2,\n   \"SubMembers\": [{\n    \"A\": 5,\n    \"B\": 6.6\n   }],\n   \"SubMembersAsObjects\": [{\n    \"A\": 5,\n    \"B\": 6.6\n   }]\n  }, {\n   \"A\": 3,\n   \"B\": 4\n  }]\n }\n}]", res);
        }
    }
}