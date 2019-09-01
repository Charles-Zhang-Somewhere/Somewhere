using Somewhere;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using YamlDotNet.Serialization;

namespace SomewhereTest
{
    public class YamlTest
    {
        [Fact]
        public void YamlDictModificationShouldReturnIntact()
        {
            // Source: https://yaml.org
            string yaml = @"What It Is: YAML is a human friendly data serialization
  standard for all programming languages.

YAML Resources:
  YAML 1.2 (3rd Edition): http://yaml.org/spec/1.2/spec.html
  YAML 1.1 (2nd Edition): http://yaml.org/spec/1.1/
  YAML 1.0 (1st Edition): http://yaml.org/spec/1.0/
  YAML Issues Page:       https://github.com/yaml/yaml/issues
  YAML Mailing List:      yaml-core@lists.sourceforge.net
  YAML IRC Channel:       ""#yaml on irc.freenode.net""
  YAML Cookbook (Ruby):   http://yaml4r.sourceforge.net/cookbook/ (local)
  YAML Reference Parser:  http://ben-kiki.org/ypaste/
  YAML Test Suite:        https://github.com/yaml/yaml-test-suite".Replace("\r", ""); // YamlDotNet will change all \r to \n, so remove it
            var oldDict = new YamlQuery(yaml).ToDictionary();
            oldDict["What It Is"] = @"YAML is a human friendly data serialization
  standard for all programming languages.".Replace("\r", ""); // i.e. not changing anything
            var newDict = new YamlQuery(new Serializer().Serialize(oldDict)).ToDictionary();
            foreach (var item in oldDict)
            {
                Assert.True(newDict.ContainsKey(item.Key));
                Assert.Equal(item.Value, newDict[item.Key]);
            }
        }

        [Fact]
        public void YamlShouldGetSingleValue()
        {
            Assert.Equal("true", new YamlQuery("Test: true").Get<string>("Test"));
            Assert.Equal(3.14, new YamlQuery("Number: 3.14").Get<double>("Number"));
        }

        [Fact]
        public void YamlShouldGetPatternInDepth()
        {
            string yaml =
@"Test:
    Key:
        Value: true";
            Assert.True(new YamlQuery(yaml).Find<bool>("Key", "Value"));
            Assert.True(new YamlQuery(yaml).On("Test")
                    .On("Key")
                    .Get("Value")
                    .ToList<bool>()
                    .Single());
        }

        [Fact]
        public void YamlShouldGetPlainDictionary()
        {
            string yaml =
@"Name: Charles
Age: 23
Gender: Male";
            var dict = new YamlQuery(yaml).ToDictionary();
            Assert.Equal("Charles", dict["Name"]);
            Assert.Equal("23", dict["Age"]);
        }

        [Fact]
        public void YamlShouldThrowExceptionWhenPropertyNotFound()
        {
            string yaml = 
@"Prop1: 15
Prop2: 25";
            Assert.Equal("15", new YamlQuery(yaml).Get<string>("Prop1"));
            Assert.Throws<ArgumentException>(() => { new YamlQuery(yaml).Get<string>("prop1"); });   // Case sensitive
            Assert.Equal(25, new YamlQuery(yaml).Get<int>("Prop2")); 
        }

        [Fact]
        public void YamlShouldSerializeAndDeserializeJournalEvent()
        {
            // Do notice default values will not be serialized
            string serialization = new Serializer().Serialize(new JournalEvent()
            {
                Operation = JournalEvent.CommitOperation.CreateNote,    // Will not be serialized
                Target = "note",
                UpdateValue = null,
                ValueFormat = JournalEvent.UpdateValueFormat.Full   // Will not be serialized
            });
            var obj = new Deserializer().Deserialize<JournalEvent>(serialization);
            Assert.Equal(JournalEvent.CommitOperation.CreateNote, obj.Operation);
            Assert.Equal("note", obj.Target);
            Assert.Null(obj.UpdateValue);
            Assert.Equal(JournalEvent.UpdateValueFormat.Full, obj.ValueFormat);

            // In this case defaults will be serialized
            serialization = new SerializerBuilder().EmitDefaults().Build().Serialize(new JournalEvent()
            {
                Operation = JournalEvent.CommitOperation.CreateNote,    // Will not be serialized
                Target = "note",
                UpdateValue = null,
                ValueFormat = JournalEvent.UpdateValueFormat.Full   // Will not be serialized
            });
            obj = new Deserializer().Deserialize<JournalEvent>(serialization);
            Assert.Equal(JournalEvent.CommitOperation.CreateNote, obj.Operation);
            Assert.Equal("note", obj.Target);
            Assert.Null(obj.UpdateValue);
            Assert.Equal(JournalEvent.UpdateValueFormat.Full, obj.ValueFormat);
        }
    }
}
