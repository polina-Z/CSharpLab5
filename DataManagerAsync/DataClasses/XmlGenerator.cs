﻿using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace ServiceLib
{
    public class XmlGenerator
    {
        readonly string outputFolder;

        public XmlGenerator(string outputFolder)
        {
            this.outputFolder = outputFolder;
        }

        public Task WriteToXmlAsync(DataSet dataSet, string fileName)
        {
            return Task.Run(() =>
            {
                dataSet.WriteXml(Path.Combine(outputFolder, $"{fileName}.xml"));

                dataSet.WriteXmlSchema(Path.Combine(outputFolder, $"{fileName}.xsd"));
            });
        }
    }
}
