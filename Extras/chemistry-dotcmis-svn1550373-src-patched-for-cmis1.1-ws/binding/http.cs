/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using DotCMIS.Enums;
using DotCMIS.Exceptions;
using DotCMIS.Util;
using System.Reflection;

namespace DotCMIS.Binding.Impl
{
    internal static class HttpUtils
    {
        public delegate void Output(Stream stream);

        public static Response InvokeGET(UrlBuilder url, BindingSession session)
        {
            return Invoke(url, "GET", null, null, session, null, null, null);
        }

        public static Response InvokeGET(UrlBuilder url, BindingSession session, long? offset, long? length)
        {
            return Invoke(url, "GET", null, null, session, offset, length, null);
        }

        public static Response InvokePOST(UrlBuilder url, String contentType, Output writer, BindingSession session)
        {
            return Invoke(url, "POST", contentType, writer, session, null, null, null);
        }

        public static Response InvokePUT(UrlBuilder url, String contentType, IDictionary<string, string> headers, Output writer, BindingSession session)
        {
            return Invoke(url, "PUT", contentType, writer, session, null, null, headers);
        }

        public static Response InvokeDELETE(UrlBuilder url, BindingSession session)
        {
            return Invoke(url, "DELETE", null, null, session, null, null, null);
        }

        private static Response Invoke(UrlBuilder url, String method, String contentType, Output writer, BindingSession session,
                long? offset, long? length, IDictionary<string, string> headers)
        {
            try
            {
                // log before connect
                if (DotCMISDebug.DotCMISSwitch.TraceInfo)
                {
                    Trace.WriteLine(method + " " + url);
                }

                // create connection           
                HttpWebRequest conn = (HttpWebRequest)WebRequest.Create(url.Url);
                conn.Method = method;
                conn.UserAgent = "Apache Chemistry DotCMIS";

                // timeouts
                int connectTimeout = session.GetValue(SessionParameter.ConnectTimeout, -2);
                if (connectTimeout >= -1)
                {
                    conn.Timeout = connectTimeout;
                }

                int readTimeout = session.GetValue(SessionParameter.ReadTimeout, -2);
                if (readTimeout >= -1)
                {
                    conn.ReadWriteTimeout = readTimeout;
                }

                // set content type
                if (contentType != null)
                {
                    conn.ContentType = contentType;
                }

                // set additional headers
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> header in headers)
                    {
                        conn.Headers.Add(header.Key, header.Value);
                    }
                }

                // authenticate
                IAuthenticationProvider authProvider = session.GetAuthenticationProvider();
                if (authProvider != null)
                {
                    conn.PreAuthenticate = true;
                    authProvider.Authenticate(conn);
                }

                // range
                if (offset != null && length != null)
                {
                    if (offset < Int32.MaxValue && offset + length - 1 < Int32.MaxValue)
                    {
                        conn.AddRange((int)offset, (int)offset + (int)length - 1);
                    }
                    else
                    {
                        try
                        {
                            MethodInfo mi = conn.GetType().GetMethod("AddRange", new Type[] { typeof(Int64), typeof(Int64) });
                            mi.Invoke(conn, new object[] { offset, offset + length - 1 });
                        }
                        catch (Exception e)
                        {
                            throw new CmisInvalidArgumentException("Offset or length too big!", e);
                        }
                    }
                }
                else if (offset != null)
                {
                    if (offset < Int32.MaxValue)
                    {
                        conn.AddRange((int)offset);
                    }
                    else
                    {
                        try
                        {
                            MethodInfo mi = conn.GetType().GetMethod("AddRange", new Type[] { typeof(Int64) });
                            mi.Invoke(conn, new object[] { offset });
                        }
                        catch (Exception e)
                        {
                            throw new CmisInvalidArgumentException("Offset too big!", e);
                        }
                    }
                }

                // compression
                string compressionFlag = session.GetValue(SessionParameter.Compression) as string;
                if (compressionFlag != null && compressionFlag.ToLower().Equals("true"))
                {
                    conn.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                }

                // send data
                if (writer != null)
                {
                    conn.SendChunked = true;
                    Stream requestStream = conn.GetRequestStream();
                    writer(requestStream);
                    requestStream.Close();
                }
                else
                {
#if __MonoCS__
                    //around for MONO HTTP DELETE issue
                    //http://stackoverflow.com/questions/11785597/monotouch-iphone-call-to-httpwebrequest-getrequeststream-connects-to-server
                    if (method == "DELETE")
                    {
                        conn.ContentLength = 0;
                        Stream requestStream = conn.GetRequestStream();
                        requestStream.Close();
                    }
#endif
                }

                // connect
                try
                {
                    HttpWebResponse response = (HttpWebResponse)conn.GetResponse();

                    if (authProvider != null)
                    {
                        authProvider.HandleResponse(response);
                    }

                    return new Response(response);
                }
                catch (WebException we)
                {
                    return new Response(we);
                }
            }
            catch (Exception e)
            {
                throw new CmisConnectionException("Cannot access " + url + ": " + e.Message, e);
            }
        }

        internal class Response
        {
            private readonly WebResponse response;

            public HttpStatusCode StatusCode { get; private set; }
            public string Message { get; private set; }
            public Stream Stream { get; private set; }
            public string ErrorContent { get; private set; }
            public string ContentType { get; private set; }
            public long? ContentLength { get; private set; }

            public Response(HttpWebResponse httpResponse)
            {
                this.response = httpResponse;
                StatusCode = httpResponse.StatusCode;
                Message = httpResponse.StatusDescription;
                ContentType = httpResponse.ContentType;
                ContentLength = httpResponse.ContentLength == -1 ? null : (long?)httpResponse.ContentLength;
                string contentTransferEncoding = httpResponse.Headers["Content-Transfer-Encoding"];
                bool isBase64 = contentTransferEncoding != null && contentTransferEncoding.Equals("base64", StringComparison.CurrentCultureIgnoreCase);

                if (httpResponse.StatusCode == HttpStatusCode.OK ||
                    httpResponse.StatusCode == HttpStatusCode.Created ||
                    httpResponse.StatusCode == HttpStatusCode.NonAuthoritativeInformation ||
                    httpResponse.StatusCode == HttpStatusCode.PartialContent)
                {
                    if (isBase64)
                    {
                        Stream = new BufferedStream(new CryptoStream(httpResponse.GetResponseStream(), new FromBase64Transform(), CryptoStreamMode.Read), 64 * 1024);
                    }
                    else
                    {
                        Stream = new BufferedStream(httpResponse.GetResponseStream(), 64 * 1024);
                    }
                }
                else
                {
                    try { httpResponse.Close(); }
                    catch (Exception) { }
                }
            }

            public Response(WebException exception)
            {
                response = exception.Response;

                HttpWebResponse httpResponse = response as HttpWebResponse;
                if (httpResponse != null)
                {
                    StatusCode = httpResponse.StatusCode;
                    Message = httpResponse.StatusDescription;
                    ContentType = httpResponse.ContentType;

                    if (ContentType != null && ContentType.ToLower().StartsWith("text/"))
                    {
                        StringBuilder sb = new StringBuilder();

                        using (StreamReader sr = new StreamReader(httpResponse.GetResponseStream()))
                        {
                            string s;
                            while ((s = sr.ReadLine()) != null)
                            {
                                sb.Append(s);
                                sb.Append('\n');
                            }
                        }

                        ErrorContent = sb.ToString();
                    }
                }
                else
                {
                    StatusCode = HttpStatusCode.InternalServerError;
                    Message = exception.Status.ToString();
                }

                try { response.Close(); }
                catch (Exception) { }
            }

            public void CloseStream()
            {
                if (Stream != null)
                {
                    Stream.Close();
                }
            }
        }
    }

    internal class UrlBuilder
    {
        private UriBuilder uri;

        public Uri Url
        {
            get { return uri.Uri; }
        }

        public UrlBuilder(string url)
        {
            if (url == null)
            {
                throw new ArgumentNullException("url");
            }

            uri = new UriBuilder(url);
        }

        public UrlBuilder AddParameter(string name, object value)
        {
            if ((name == null) || (value == null))
            {
                return this;
            }

            string valueStr = Uri.EscapeDataString(UrlBuilder.NormalizeParameter(value));

            if (uri.Query != null && uri.Query.Length > 1)
            {
                uri.Query = uri.Query.Substring(1) + "&" + name + "=" + valueStr;
            }
            else
            {
                uri.Query = name + "=" + valueStr;
            }

            return this;
        }

        public static string NormalizeParameter(object value)
        {
            if (value == null)
            {
                return null;
            }
            else if (value is Enum)
            {
                return ((Enum)value).GetCmisValue();
            }
            else if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }

            return value.ToString();
        }

        public override string ToString()
        {
            return Url.ToString();
        }
    }

    internal class MimeHelper
    {
        public const string ContentDisposition = "Content-Disposition";
        public const string DispositionAttachment = "attachment";
        public const string DispositionFilename = "filename";

        private const string MIMESpecials = "()<>@,;:\\\"/[]?=" + "\t ";
        private const string RFC2231Specials = "*'%" + MIMESpecials;
        private static char[] HexDigits = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

        public static string EncodeContentDisposition(string disposition, string filename)
        {
            if (disposition == null)
            {
                disposition = DispositionAttachment;
            }
            return disposition + EncodeRFC2231(DispositionFilename, filename);
        }

        protected static string EncodeRFC2231(string key, string value)
        {
            StringBuilder buf = new StringBuilder();
            bool encoded = EncodeRFC2231value(value, buf);
            if (encoded)
            {
                return "; " + key + "*=" + buf.ToString();
            }
            else
            {
                return "; " + key + "=" + value;
            }
        }

        protected static bool EncodeRFC2231value(string value, StringBuilder buf)
        {
            buf.Append("UTF-8");
            buf.Append("''"); // no language
            byte[] bytes;
            try
            {
                bytes = UTF8Encoding.UTF8.GetBytes(value);
            }
            catch (Exception)
            {
                return true;
            }

            bool encoded = false;
            for (int i = 0; i < bytes.Length; i++)
            {
                int ch = bytes[i] & 0xff;
                if (ch <= 32 || ch >= 127 || RFC2231Specials.IndexOf((char)ch) != -1)
                {
                    buf.Append('%');
                    buf.Append(HexDigits[ch >> 4]);
                    buf.Append(HexDigits[ch & 0xf]);
                    encoded = true;
                }
                else
                {
                    buf.Append((char)ch);
                }
            }
            return encoded;
        }
    }
}
