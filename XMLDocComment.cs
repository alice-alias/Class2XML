using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml.Linq;
using System.IO;

namespace Class2XML
{

    class XMLDocCommentList : IEnumerable<XMLDocCommentMember>
    {
        List<XMLDocComment> docs = new List<XMLDocComment>();

        public void Add(XMLDocComment item) { docs.Add(item); }
        public void AddRange(IEnumerable<XMLDocComment> items) { docs.AddRange(items); }
        public void Clear() { docs.Clear(); }
        public bool Contains(XMLDocComment item) { return docs.Contains(item); }
        public bool Contains(XMLDocCommentMember item) { return docs.Any(x => x.Members.Contains(item)); }
        public bool Contains(string name) { return this.Any(x => x.Name == name); }

        public XMLDocCommentMember this[string name]
        {
            get {
                return this.FirstOrDefault(x => x.Name == name);
            }
        }

        public int Count { get { return docs.Select(x => x.Members).Count(); } }

        public bool Remove(XMLDocComment item) { return docs.Remove(item); }
        
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<XMLDocCommentMember> GetEnumerator()
        {
            return docs.SelectMany(x => x.Members).GetEnumerator();
        }
    }

    class XMLDocComment
    {
        public string FileName { get; private set; }
        public string AssemblyName { get; private set; }

        public string Display { get { return string.Format("{0}; {1}", AssemblyName, FileName); } }

        public XMLDocCommentMember[] Members { get; private set; }

        public XMLDocComment(XDocument xml)
        {
            FileName = "";

            var asm = xml.Root.Element("assembly");
            try
            {
                AssemblyName = xml.Root.Element("assembly").Element("name").Value;
            }
            catch
            {
                AssemblyName = xml.Root.Element("assembly").Value.Trim().Trim('"');
            }

            Members = xml.Root.Element("members").Elements("member").Select(x => new XMLDocCommentMember(x)).ToArray();
        }

        public static XMLDocComment Load(string path)
        {
            using (var stream = File.OpenRead(path))
                return new XMLDocComment(XDocument.Load(stream)) { FileName = path };
        }
    }

    class XMLDocCommentMember
    {
        public XElement XElement { get; private set; }

        public XMLDocCommentMember(XElement element) { XElement = element; }

        public String Name { get { return XElement.Attribute("name").Value; } }
    }
}
