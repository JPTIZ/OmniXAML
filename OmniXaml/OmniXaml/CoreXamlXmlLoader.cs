﻿namespace OmniXaml
{
    using System.Collections.Generic;
    using System.IO;
    using Glass;
    using Parsers.ProtoParser.SuperProtoParser;
    using Parsers.XamlNodes;

    public class CoreXamlXmlLoader : ICoreXamlLoader
    {
        private readonly IObjectAssembler objectAssembler;
        private readonly WiringContext wiringContext;

        public CoreXamlXmlLoader(IObjectAssembler objectAssembler, WiringContext wiringContext)            
        {
            Guard.ThrowIfNull(objectAssembler, nameof(objectAssembler));
            Guard.ThrowIfNull(wiringContext, nameof(wiringContext));

            this.objectAssembler = objectAssembler;
            this.wiringContext = wiringContext;
        }

        public object Load(Stream stream)
        {
            using (var t = new StreamReader(stream))
            {
                return Load(t.ReadToEnd());
            }
        }

        private object Load(string xml)
        {
            var pullParser = new XamlNodesPullParser(wiringContext);
            var protoXamlNodes = new SuperProtoParser(wiringContext).Parse(xml);
            var xamlNodes = pullParser.Parse(protoXamlNodes);
            return Load(xamlNodes);
        }

        private object Load(IEnumerable<XamlNode> xamlNodes)
        {
            foreach (var xamlNode in xamlNodes)
            {
                objectAssembler.Process(xamlNode);
            }

            return objectAssembler.Result;
        }
    }
}