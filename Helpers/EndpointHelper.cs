using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Dynamicweb.DataIntegration.EndpointManagement;
using Dynamicweb.Configuration;
using Dynamicweb.Logging;

using Dynamicweb.MMT.Custom.Shipping.Models;

namespace Dynamicweb.MMT.Custom.Shipping.Helpers
{
    public class EndpointHelper
    {
        public readonly int _requestTimeout;
        public readonly ILogger _logger;
        public EndpointAuthentication _endpointAuthentication;
        public EndpointHelper(int requestTimeout, ILogger logger, int endpointAuthenticationId)
        {
            _requestTimeout = requestTimeout;
            EndpointAuthenticationService endpointAuthenticationService = new EndpointAuthenticationService();
            _endpointAuthentication = endpointAuthenticationService.GetEndpointAuthenticationById(endpointAuthenticationId);
            _logger = logger;
        }
        public List<T> PostToBC<T>(string url, string jsonObject, string oDataEtag = null, bool IsReturnObject = false)
        {
            var _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(_requestTimeout);

            string token = OAuthHelper.GetToken(null, _endpointAuthentication);

            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                    { "accept", "application/json" },
                    { "Content-Type", "application/json" },
                    { "authorization", "Bearer " + (token ?? throw new ArgumentNullException(nameof(token))) }
            };
            var method = HttpMethod.Post;
            if (!string.IsNullOrEmpty(oDataEtag))
            {
                headers.Add("If-Match", oDataEtag);
                method = new HttpMethod("PATCH");
            }
            var msg = CreateRequestMessage(method, url, headers);

            using (var content1 = new StringContent(jsonObject))
            {
                AddContentHeaders(content1, headers);
                msg.Content = content1;

                Task<HttpResponseMessage> awaitPostResponseFromBC = _client.SendAsync(msg);
                awaitPostResponseFromBC.Wait();
                using (var content = awaitPostResponseFromBC.Result.Content)
                {
                    var response = awaitPostResponseFromBC.Result;
                    // Retrieve HTTP headers, both response and content headers.
                    var responseHeaders = GetHeaders(awaitPostResponseFromBC.Result, content);

                    // Retrieve actual content.
                    Task<string> awaitResponseContent = content.ReadAsStringAsync();
                    awaitResponseContent.Wait();
                    string responseContent = awaitResponseContent.Result;

                    // Check if request was successful, and if not, throw an exception.
                    if (!response.IsSuccessStatusCode)
                    {
                        var responseResult = new RestResponse<ResponseFromBC<T>>
                        {
                            Error = responseContent,
                            Status = response.StatusCode,
                            Headers = responseHeaders,
                        };
                        //_logger?.Error($"ERP error: {responseContent} Status: {response.StatusCode} Headers: {responseHeaders} URL: {url}");
                        throw new Exception(responseContent);
                    }
                    else
                    {
                        //_logger?.Info($"Send success");
                        var responseResult = new RestResponse<ResponseFromBC<T>>
                        {
                            Status = response.StatusCode,
                            Headers = responseHeaders
                        };

                        // Check if caller wants a string type of return
                        if (typeof(T) == typeof(string))
                        {
                            List<T> list = new List<T>();
                            list.Add((T)(object)responseContent);
                            return list;
                        }
                        else if (typeof(IConvertible).IsAssignableFrom(typeof(ResponseFromBC<T>)))
                        {
                            /*
                             * Check if Response type implements IConvertible, at which point we simply convert the
                             * response instead of parsing it using JSON conversion.
                             *
                             * This might be used if caller is requesting an integer, or some other object
                             * that has automatic conversion from string to itself.
                             */
                            responseResult.Content = (ResponseFromBC<T>)Convert.ChangeType(responseContent, typeof(ResponseFromBC<T>));
                        }
                        else
                        {
                            /*
                             * Check if the caller is interested in some sort of JContainer, such as a JArray or JObject,
                             * at which point we simply return the above object immediately as such.
                             */
                            var objResult = JToken.Parse(responseContent);
                            if (typeof(ResponseFromBC<ResponseFromBC<T>>) == typeof(JContainer))
                            {
                                responseResult.Content = (ResponseFromBC<T>)(object)objResult;
                            }

                            if (IsReturnObject)
                            {
                                return new List<T>() { objResult.ToObject<T>() };
                            }

                            //Converting above JContainer to instance of requested type, and returns object to caller.
                            responseResult.Content = objResult.ToObject<ResponseFromBC<T>>();
                        }

                        // Finally, we can return result to caller.
                        if (responseResult.Content != null)
                            return responseResult.Content.Value;
                    }
                }
            }

            return new List<T>();
        }

        public Stream GetFileFromBC(string url)
        {
            var _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(_requestTimeout);

            string token = OAuthHelper.GetToken(null, _endpointAuthentication);

            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                    { "authorization", "Bearer " + (token ?? throw new ArgumentNullException(nameof(token))) }
            };
            var msg = CreateRequestMessage(HttpMethod.Get, url, headers);
            Task<HttpResponseMessage> awaitPostResponseFromBC = _client.SendAsync(msg);
            awaitPostResponseFromBC.Wait();
            var response = awaitPostResponseFromBC.Result;

            using (var content = awaitPostResponseFromBC.Result.Content)
            {
                // Retrieve HTTP headers, both response and content headers.
                var responseHeaders = GetHeaders(response, content);

                // Clone the stream since we will be using this in synchrounous methods earlier in the callstack meaning we cannot have the stream disposed too soon
                Stream contentStream = new MemoryStream();
                content.CopyToAsync(contentStream).Wait();
                contentStream.Position = 0;

                // Check if request was successful, and if not, throw an exception.
                if (!response.IsSuccessStatusCode)
                {
                    //string statusText = content.ReadAsStringAsync();
                    _logger?.Error($"HttpRestClient: '{url}' returned {response.StatusCode}");
                    return null;
                }
                else
                {
                    return contentStream;
                }
            }
        }

        public List<T> GetFromBC<T>(string url, bool IsReturnObject = false)
        {
            var _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(_requestTimeout);

            string token = OAuthHelper.GetToken(null, _endpointAuthentication);

            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                    { "authorization", "Bearer " + (token ?? throw new ArgumentNullException(nameof(token))) }
            };
            var msg = CreateRequestMessage(HttpMethod.Get, url, headers);
            Task<HttpResponseMessage> awaitPostResponseFromBC = _client.SendAsync(msg);
            awaitPostResponseFromBC.Wait();
            var response = awaitPostResponseFromBC.Result;
            using (var content = response.Content)
            {
                // Retrieve HTTP headers, both response and content headers.
                var responseHeaders = GetHeaders(response, content);

                // Retrieve actual content.
                Task<string> awaitResponseContent = content.ReadAsStringAsync();
                awaitResponseContent.Wait();
                string responseContent = awaitResponseContent.Result;

                // Check if request was successful, and if not, throw an exception.
                if (!response.IsSuccessStatusCode)
                {
                    var responseResult = new RestResponse<ResponseFromBC<T>>
                    {
                        Error = responseContent,
                        Status = response.StatusCode,
                        Headers = responseHeaders,
                    };
                    //_logger?.Error($"ERP error: {responseContent} Status: {response.StatusCode} Headers: {responseHeaders} URL: {url}");
                    throw new Exception(responseContent);
                }
                else
                {
                    //_logger?.Info($"Send success");
                    var responseResult = new RestResponse<ResponseFromBC<T>>
                    {
                        Status = response.StatusCode,
                        Headers = responseHeaders
                    };

                    // Check if caller wants a string type of return
                    if (typeof(ResponseFromBC<T>) == typeof(string))
                    {
                        responseResult.Content = (ResponseFromBC<T>)(object)responseContent;
                    }
                    else if (typeof(IConvertible).IsAssignableFrom(typeof(ResponseFromBC<T>)))
                    {
                        /*
                         * Check if Response type implements IConvertible, at which point we simply convert the
                         * response instead of parsing it using JSON conversion.
                         *
                         * This might be used if caller is requesting an integer, or some other object
                         * that has automatic conversion from string to itself.
                         */
                        responseResult.Content = (ResponseFromBC<T>)Convert.ChangeType(responseContent, typeof(ResponseFromBC<T>));
                    }
                    else
                    {
                        /*
                         * Check if the caller is interested in some sort of JContainer, such as a JArray or JObject,
                         * at which point we simply return the above object immediately as such.
                         */
                        var objResult = JToken.Parse(responseContent);
                        if (typeof(ResponseFromBC<ResponseFromBC<T>>) == typeof(JContainer))
                        {
                            responseResult.Content = (ResponseFromBC<T>)(object)objResult;
                        }

                        if (IsReturnObject)
                        {
                            return new List<T>() { objResult.ToObject<T>() };
                        }

                        //Converting above JContainer to instance of requested type, and returns object to caller.
                        responseResult.Content = objResult.ToObject<ResponseFromBC<T>>();
                    }

                    // Finally, we can return result to caller.
                    if (responseResult.Content != null)
                        return responseResult.Content.Value;
                }
            }
            return new List<T>();
        }
        public List<T> DeleteFromBC<T>(string URL)
        {/*
            var metadataUri = new Uri(URL);
            var credentialCache = new CredentialCache();
            credentialCache.Add(new Uri(metadataUri.GetLeftPart(UriPartial.Authority)), _endpointAuthentication.Type.ToString(), _endpointAuthentication.GetNetworkCredential());
            var _client = new HttpRestClient(credentialCache, _requestTimeout, _logger);
            Task<RestResponse<T>> awaitResponseFromBC;
            awaitResponseFromBC = _client.DeleteAsync<T>(URL, OAuthHelper.GetToken(null, _endpointAuthentication));
            return awaitResponseFromBC;*/
            return new List<T>();
        }
        internal class ResponseFromBC<T>
        {
            [JsonProperty("@odata.context")]
            public string Odata { get; set; }
            [JsonProperty("value")]
            public List<T> Value { get; set; }
        }

        private static Dictionary<string, string> GetHeaders(HttpResponseMessage response, HttpContent content)
        {
            var headers = new Dictionary<string, string>();
            foreach (var idx in response.Headers)
            {
                headers.Add(idx.Key, string.Join(";", idx.Value));
            }
            foreach (var idx in content.Headers)
            {
                headers.Add(idx.Key, string.Join(";", idx.Value));
            }

            return headers;
        }

        //Create a new request message, and decorates it with the relevant HTTP headers.
        private static HttpRequestMessage CreateRequestMessage(HttpMethod method, string url, Dictionary<string, string> headers)
        {
            var msg = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = method
            };

            foreach (var idx in headers.Keys)
            {
                /*
                 * We ignore all headers that belongs to the content, and add all other headers to the request.
                 * This is because all HttpContent headers are added later, but only if content is being transmitted.
                 * This allows support for any HTTP headers, including custom headers.
                 */
                switch (idx)
                {
                    case "Allow":
                    case "Content-Disposition":
                    case "Content-Encoding":
                    case "Content-Language":
                    case "Content-Length":
                    case "Content-Location":
                    case "Content-MD5":
                    case "Content-Range":
                    case "Content-Type":
                    case "Expires":
                    case "Last-Modified":
                        break;
                    default:
                        msg.Headers.Add(idx, headers[idx]);
                        break;
                }
            }
            return msg;
        }

        // Decorate the HTTP content with the relevant HTTP headers from the specified dictionary.
        private static void AddContentHeaders(HttpContent content, Dictionary<string, string> headers)
        {
            foreach (var idx in headers.Keys)
            {
                // Adding all Content HTTP headers, and ignoring the rest
                switch (idx)
                {
                    case "Allow":
                    case "Content-Disposition":
                    case "Content-Encoding":
                    case "Content-Language":
                    case "Content-Length":
                    case "Content-Location":
                    case "Content-MD5":
                    case "Content-Range":
                    case "Content-Type":
                    case "Expires":
                    case "Last-Modified":
                        if (content.Headers.Contains(idx))
                        {
                            content.Headers.Remove(idx);
                        }

                        content.Headers.Add(idx, headers[idx]);
                        break;
                }
            }
        }


        public string SendRequestBasic(string url, string token, string data)
        {
            var _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(_requestTimeout);

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers = new Dictionary<string, string>
            {
                { "Accept", "application/json" },
                { "Content-Type", "application/json" },
            };

            // add authorisation token if set
            if (!string.IsNullOrEmpty(token))
            {
                headers.Add("Authorization", "Bearer " + token);
            }

            var msg = CreateRequestMessage(string.IsNullOrEmpty(data) ? HttpMethod.Get : HttpMethod.Post, url, headers);
            // add post data
            if (!string.IsNullOrEmpty(data))
            {
                var content1 = new StringContent(data);
                AddContentHeaders(content1, headers);
                msg.Content = content1;
            }

            Task<HttpResponseMessage> awaitResponse = _client.SendAsync(msg);
            awaitResponse.Wait();
            var response = awaitResponse.Result;

            using (var content = response.Content)
            {
                // Retrieve HTTP headers, both response and content headers.
                var responseHeaders = GetHeaders(response, content);

                // Retrieve actual content.
                Task<string> awaitResponseContent = content.ReadAsStringAsync();
                awaitResponseContent.Wait();
                string responseContent = awaitResponseContent.Result;

                // Check if request was successful, and if not, throw an exception.
                if (!response.IsSuccessStatusCode)
                {
                    //string statusText = content.ReadAsStringAsync();
                    _logger?.Error($"HttpRestClient: '{url}' returned {response.StatusCode}");
                    return null;
                }
                else
                {
                    return responseContent;
                }
            }
        }
    }
}
