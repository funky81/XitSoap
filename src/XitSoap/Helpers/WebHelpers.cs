using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace HodStudio.XitSoap.Helpers
{
    public static class XmlExtensions
    {
        /// <summary>
        /// Returns the specified <see cref="XElement"/>
        /// without namespace qualifiers on elements and attributes.
        /// </summary>
        /// <param name="element">The element</param>
        public static void StripNamespace(this XDocument document)
        {
            if (document.Root == null) return;

            foreach (var element in document.Root.DescendantsAndSelf())
            {
                element.Name = element.Name.LocalName;
                element.ReplaceAttributes(GetAttributesWithoutNamespace(element));
            }
        }
        static IEnumerable GetAttributesWithoutNamespace(XElement xElement)
        {
            return xElement.Attributes()
                .Where(x => !x.IsNamespaceDeclaration)
                .Select(x => new XAttribute(x.Name.LocalName, x.Value));
        }
    }
    internal static class WebHelpers
    {
        internal static void ExtractResult(this WebService service, string methodName)
        {
            foreach (XElement element in service.Result.SoapResponse.Descendants(methodName + "Response"))
            {
                if (element.NodeType == XmlNodeType.Element)
                {
                    XDocument doc = new XDocument();
                    var rootElement = new XElement("root");
                    var i = 0;
                    foreach (var inner in element.Descendants())
                    {
                        if (i != 0)
                        {
                            rootElement.Add(inner);
                        }
                        i++;
                    }
                    doc.Add(rootElement);
                    service.Result.XmlResult = doc;
                    service.Result.StringResult = doc.ToString();
                }
                else
                {
                    service.Result.StringResult = element.FirstNode.ToString();
                    service.Result.XmlResult = XDocument.Parse(string.Format(StringConstants.XmlResultXDocumentFormat, service.Result.StringResult));
                }
            }
        }
        /// <summary>
        /// Invokes a Web Method, with its parameters encoded or not.
        /// </summary>
        /// <param name="methodName">Name of the web method you want to call (case sensitive)</param>
        /// <param name="encode">Do you want to encode your parameters? (default: true)</param>
        internal static void InvokeService(this WebService service, string methodName, bool encode, string soapActionComplement)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(service.Url);
            req.Headers.Add(StringConstants.SoapHeaderName, CreateSoapHeaderName(service.Namespace, methodName, soapActionComplement));
            req.ContentType = StringConstants.SoapContentType;
            req.Accept = StringConstants.SoapAccept;
            req.Method = StringConstants.SoapMethod;

            req.CookieContainer = service.CookieContainer;

            if (service.AuthenticationInfo != null)
            {
                req.PreAuthenticate = true;
                req.Headers.Add("Authorization", service.AuthenticationInfo.AuthenticationHeader);
            }

            foreach (var item in service.Headers)
            {
                req.Headers.Add(item.Key, item.Value);
            }

            var postValues = new StringBuilder();
            foreach (var param in service.Parameters)
            {
                if (encode) postValues.AppendFormat(StringConstants.SoapParamFormat, HttpUtility.HtmlEncode(param.Key), HttpUtility.HtmlEncode(param.Value));
                else postValues.AppendFormat(StringConstants.SoapParamFormat, param.Key, param.Value);
            }
            postValues = service.ParametersMappers.ApplyMappers(postValues);

            var soapStr = string.Format(StringConstants.SoapStringFormat, methodName, postValues.ToString(), service.Namespace);

            var stm = req.GetRequestStream();
            using (StreamWriter stmw = new StreamWriter(stm))
                stmw.Write(soapStr);

            stm.Close();

            var responseReader = new StreamReader(req.GetResponse().GetResponseStream());
            string result = responseReader.ReadToEnd();
            service.Result.SoapResponse = XDocument.Parse(result);
            service.Result.SoapResponse.StripNamespace();
            service.ExtractResult(methodName);
            responseReader.Close();
        }

        private static string CreateSoapHeaderName(string @namespace, string methodName, string soapActionComplement)
        {
            var fixedNamespace = @namespace;
            var soapComplement = string.Empty;
            if (!string.IsNullOrEmpty(soapActionComplement))
                soapComplement = soapActionComplement + "/";

            if (fixedNamespace.EndsWith("/"))
                fixedNamespace = fixedNamespace.Substring(0, fixedNamespace.Length - 1);
            return string.Format(StringConstants.SoapHeaderFormat, fixedNamespace, soapComplement, methodName);
        }
    }
}
