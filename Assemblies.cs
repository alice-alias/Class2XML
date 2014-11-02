using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel;

using System.Collections.ObjectModel;

using System.Reflection;
using System.Xml.Linq;

namespace Class2XML
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    abstract class MemberItem
    {
        public MemberItem(XMLDocCommentList doc = null) { Document = doc; }

        public XMLDocCommentList Document { get; private set; }

        abstract public string Name { get; }
        /// <summary>XML ドキュメント コメント で使用される名前</summary>
        abstract public string XMLDocName { get; }

        [Browsable(false)]
        abstract public MemberItem[] Children { get; }

        public virtual XElement Description { get { return Document.Contains(XMLDocName) ? Document[XMLDocName].XElement : null; } }
        public virtual IEnumerable<XNode> DescriptionBody { get { return Description != null ? Description.Nodes() : null; } }

        public abstract XElement CreateXML();
    }

    class NamespaceInfo : MemberItem
    {
        string name;
        public override string Name { get { return name; } }
        public override string XMLDocName { get { return "N:" + Name; } }

        public NamespaceInfo(string name, IEnumerable<Assembly> asms, XMLDocCommentList doc = null)
            : this(name, asms.SelectMany(x => x.GetTypes().Where(y => y.Namespace == name && TypeInfo.CheckVisibleType(y))).ToArray(), doc) { }

        private NamespaceInfo(string name, IEnumerable<Type> asms, XMLDocCommentList doc = null)
            : base(doc)
        {
            this.name = name;
            children = asms.Select(x => TypeInfo.Create(x, doc)).ToArray();
        }

        MemberItem[] children;
        public override MemberItem[] Children { get { return children; } }

        public static NamespaceInfo[] Create(IEnumerable<Assembly> asms, XMLDocCommentList doc = null)
        {
            return Create(asms.SelectMany(x => x.GetTypes().Where(y => TypeInfo.CheckVisibleType(y))), doc);
        }

        public static NamespaceInfo[] Create(IEnumerable<Type> types, XMLDocCommentList doc = null)
        {
            var dic = new Dictionary<string, List<Type>>();
            foreach (var t in types)
            {
                if (!dic.ContainsKey(t.Namespace)) dic.Add(t.Namespace, new List<Type>());
                dic[t.Namespace].Add(t);
            }
            return dic.Select(x => new NamespaceInfo(x.Key, x.Value, doc)).ToArray();
        }

        public override string ToString() { return Name; }

        public override XElement CreateXML()
        {
            var elm = new XElement("namespace");
            elm.SetAttributeValue("name", Name);

            if (DescriptionBody != null)
                elm.Add(new XElement("description", DescriptionBody));

            elm.Add(new XElement("types", Children.Select(x => x.CreateXML())));

            return elm;
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    class AttributeInfo
    {
        XMLDocCommentList doc;
        public CustomAttributeData AttributeData { get; private set; }
        public AttributeInfo(CustomAttributeData attrdata, XMLDocCommentList doc = null)
        {
            this.doc = doc;
            AttributeData = attrdata;
        }

        public TypeInfo Type { get { return TypeInfo.Create(AttributeData.AttributeType, doc); } }

        public AttributeParameter[] Parameters
        {
            get
            {
                return AttributeParameter.Create(AttributeData.ConstructorArguments, doc).Concat(AttributeData.NamedArguments.Select(x => new AttributeNamedParameter(x, doc))).ToArray();
            }
        }

        public XElement CreateXML()
        {
            return new XElement("attribute", new XAttribute("type", Type.DisplayFullName), Parameters.Select(x => x.CreateXML()));
        }
        
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    class AttributeParameter
    {
        XMLDocCommentList doc;

        protected AttributeParameter(Type type, Object value, XMLDocCommentList doc = null)
        {
            this.doc = doc;
            TypedValue = value;
            if (type.IsSubclassOf(typeof(Enum)))
                TypedValue = Enum.ToObject(type, TypedValue);
            Type = TypeInfo.Create(type, doc);
        }

        public TypeInfo Type { get; private set; }
        public Object TypedValue { get; private set; }

        public static IEnumerable<AttributeParameter> Create(IEnumerable<CustomAttributeTypedArgument> args, XMLDocCommentList doc = null)
        {
            return args.Select(x => Create(x, doc)).SelectMany(_ => _);
        }

        public static  IEnumerable<AttributeParameter> Create(CustomAttributeTypedArgument arg, XMLDocCommentList doc = null)
        {
            if (arg.Value is System.Collections.IEnumerable && !(arg.Value is string))
            {
                foreach (var a in (System.Collections.IEnumerable)arg.Value)
                    yield return new AttributeParameter(arg.ArgumentType, a, doc);
            }
            else
            {
                yield return new AttributeParameter(arg.ArgumentType, arg.Value, doc);
            }
        }

        public virtual XElement CreateXML()
        {
            if(TypedValue is Type)
                return new XElement("argument", new XAttribute("type", Type.DisplayFullName), new XAttribute("value", TypeInfo.Create((Type)TypedValue).DisplayFullName));
            return new XElement("argument", new XAttribute("type", Type.DisplayFullName), new XAttribute("value", TypedValue.ToString()));
        }
    }

    class AttributeNamedParameter : AttributeParameter
    {
        public string MemberName { get; private set; }
        public AttributeNamedParameter(CustomAttributeNamedArgument arg, XMLDocCommentList doc = null) 
            : base(arg.TypedValue.ArgumentType, arg.TypedValue.Value, doc)
        {
            MemberName = arg.MemberName;
        }

        public override XElement CreateXML()
        {
            var elm = base.CreateXML();
            elm.Add(new XAttribute("name", MemberName));
            return elm;
        }
    }
    
    class TypeInfo : MemberItem
    {
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public Type Type { get; protected set; }

        public override string Name { get { return Type.Name; } }
        virtual public string FullName { get { return Type.FullName; } }
        public override string XMLDocName { get { return "T:" + FullName; } }

        public static bool CheckVisibleType(Type type)
        {
            return type.IsVisible && !type.IsNested;
        }

        protected TypeInfo(Type type, XMLDocCommentList doc = null) : base(doc) { 
            Type = type;
        }
        
        public AttributeInfo[] Attributes { 
            get { 
                return Type.GetCustomAttributesData().Select(x => new AttributeInfo(x, Document)).ToArray();
            }
        }

        public static TypeInfo Create(Type type, XMLDocCommentList doc = null)
        {
            if (type.IsSubclassOf(typeof(System.Delegate))) return new DelegateInfo(type, doc);
            if (type.IsClass) return new ClassInfo(type, doc);

            if (type.IsInterface) return new InterfaceInfo(type, doc);

            if (type.IsEnum) return new EnumInfo(type, doc);
            if (type.IsValueType) return new ValueTypeInfo (type, doc);

            return new TypeInfo(type, doc);
        }

        public override string ToString() { return DisplayFullName; }

        public virtual string DisplayName
        {
            get
            {
                if (!Type.IsGenericType)
                    return Type.Name;

                return Type.Name.Split('`')[0] + "<" + string.Join(",", Type.GetGenericArguments().Select(x => x.IsGenericParameter ? x.Name : TypeInfo.Create(x).DisplayFullName)) + ">";
            }
        }

        public virtual string DisplayFullName { 
            get {
                var path = DisplayName;

                var t = Type;
                while (t.IsNested)
                {
                    t = t.DeclaringType;
                    path = TypeInfo.Create(t).DisplayName + "." + path;
                }

                return Type.Namespace + "." + path;
            }
        }

        public override MemberItem[] Children
        {
            get
            {
                return new MemberItem[0];
            }
        }

        public String AssemblyName { get { return AssemblyFullName.Split(',')[0]; } }
        public String AssemblyFullName { get { return Type.Assembly.FullName; } }
        public String AssemblyFileName { get { return System.IO.Path.GetFileName(Type.Assembly.Location); } }

        protected String ElementName { get; set; }

        public override XElement CreateXML()
        {
            var elm = new XElement(ElementName, new XAttribute("name", DisplayName), new XAttribute("doc-name", XMLDocName));

            elm.Add(new XElement("assembly",
                new XAttribute("name", AssemblyFullName),
                new XAttribute("file", AssemblyFileName)));

            if(Attributes.Length > 0)
                elm.Add(new XElement("attributes", Attributes.Select(x => x.CreateXML())));

            if (DescriptionBody != null)
                elm.Add(new XElement("description", DescriptionBody));

            return elm;
        }
    }

    class DelegateInfo : TypeInfo
    {
        MethodInfo method;
        public DelegateInfo(Type type, XMLDocCommentList doc = null) : base(type, doc) {
            method = ((Delegate)Activator.CreateInstance(type)).GetMethodInfo();
            ElementName = "delegate";
        }
    }

    class EnumInfo : TypeInfo
    {
        public EnumInfo(Type type, XMLDocCommentList doc = null)
            : base(type, doc)
        {
            ElementName = "enum";
        }

        public string[] Names { get { return Enum.GetNames(Type); } }
        public TypeInfo UnderlyingType { get { return TypeInfo.Create(Enum.GetUnderlyingType(Type)); } }

        public override XElement CreateXML()
        {
            var elm = base.CreateXML();

            elm.Add(new XElement("values", Names.Select(x =>
            {
                XElement doc = null;
                if (Document["F:" + this.FullName + "." + x] != null)
                    doc = new XElement("description", Document["F:" + this.FullName + "." + x].Nodes);
                return new XElement("value", new XAttribute("name", x), doc);
            })));
            
            return elm;
        }
    }

    class ValueTypeInfo : TypeInfo
    {
        public ValueTypeInfo(Type type, XMLDocCommentList doc = null) : base(type, doc) {
            ElementName = "struct";
        }
    }

    class InterfaceInfo : TypeInfo
    {
        public InterfaceInfo(Type type, XMLDocCommentList doc = null)
            : base(type, doc)
        {
            ElementName = "interface";
        }

        public InterfaceInfo[] Extends { get { return GetDirectInterfaces(Type).Select(x => (InterfaceInfo)TypeInfo.Create(x)).ToArray(); } }

        public override XElement CreateXML()
        {
            var elm = base.CreateXML();
            elm.Add(CreateBaseXML());
            return elm;
        }

        public static IEnumerable<Type> GetDirectInterfaces(Type type)
        {

            IEnumerable<Type> ifs = type.GetInterfaces();

            IEnumerable<Type> ign = ifs.SelectMany(x => x.GetInterfaces());
            if (type.BaseType != null)
                ign = ign.Concat(type.BaseType.GetInterfaces());

            return ifs.Except(ign);
        }

        public IEnumerable<XElement> CreateBaseXML()
        {
            return Extends.Select(x => new XElement("extends", new XAttribute("name", x.DisplayFullName), x.CreateBaseXML()));
        }

    }

    class ClassInfo : TypeInfo
    {
        public ClassInfo(Type type, XMLDocCommentList doc = null)
            : base(type, doc)
        {
            ElementName = "class";
        }

        public ClassInfo Extends { get { return Type.BaseType != null ? (ClassInfo)TypeInfo.Create(Type.BaseType, Document) : null; } }
        public InterfaceInfo[] Implements { get { return InterfaceInfo.GetDirectInterfaces(Type).Select(x => (InterfaceInfo)TypeInfo.Create(x, Document)).ToArray(); } }

        public bool IsStatic { get { return Type.IsAbstract && Type.IsSealed; } }
        public bool IsAbstract { get { return Type.IsAbstract && !IsStatic; } }
        public bool IsSealed { get { return Type.IsSealed && !IsStatic; } }

        public override XElement CreateXML()
        {
            var elm = base.CreateXML();
            if (IsStatic) elm.SetAttributeValue("static", "true");
            if (IsAbstract) elm.SetAttributeValue("abstract", "true");
            if (IsSealed) elm.SetAttributeValue("sealed", "true");

            elm.Add(CreateBaseXML());

            return elm;
        }

        public IEnumerable<XElement> CreateBaseXML()
        {
            if (Extends != null)
                yield return new XElement("extends", new XAttribute("name", Extends.DisplayFullName), Extends.CreateBaseXML());

            foreach (var i in Implements.Select(x => new XElement("implements", new XAttribute("name", x.DisplayFullName), x.CreateBaseXML())))
                yield return i;
        }
    }


}
