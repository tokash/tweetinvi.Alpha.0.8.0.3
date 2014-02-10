using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using TweetinCore.Enum;
using TweetinCore.Events;
using TweetinCore.Interfaces.oAuth;
using oAuthConnection.Utils;
using Tweetinvi;

namespace oAuthConnection
{
    /// <summary>
    /// Generate a Token that can be used to perform OAuth queries
    /// </summary>
    public class OAuthToken : IOAuthToken
    {
        #region Attributes

        /// <summary>
        /// Object used to generate the HttpWebRequest based on parameters
        /// </summary>
        private readonly OAuthWebRequestGenerator _queryGenerator;

        #endregion

        #region Properties

        /// <summary>
        /// Credentials used by the Token to create queries
        /// </summary>
        public virtual IOAuthCredentials Credentials { get; set; }
        
        /// <summary>
        /// Headers of the latest WebResponse
        /// </summary>
        protected WebHeaderCollection _lastHeadersResult { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Generates a Token with credentials of both user and consumer
        /// </summary>
        /// <param name="accessToken">Client token key</param>
        /// <param name="accessTokenSecret">Client token secret key</param>
        /// <param name="consumerKey">Consumer Key</param>
        /// <param name="consumerSecret">Consumer Secret Key</param>
        public OAuthToken(
            string accessToken,
            string accessTokenSecret,
            string consumerKey,
            string consumerSecret)
            : this(new OAuthCredentials(accessToken, accessTokenSecret, consumerKey, consumerSecret))
        {
        }

        /// <summary>
        /// Generates a Token with a specific OAuthCredentials
        /// </summary>
        /// <param name="credentials">Credentials object</param>
        public OAuthToken(IOAuthCredentials credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException("credentials");
            }

            Credentials = credentials;
            _queryGenerator = new OAuthWebRequestGenerator();
        }

        #endregion

        #region IOAuthToken Members

        public virtual IEnumerable<IOAuthQueryParameter> GenerateParameters()
        {
            List<IOAuthQueryParameter> headers = new List<IOAuthQueryParameter>();
            // Add Header for every connection to a Twitter Application
            if (!String.IsNullOrEmpty(Credentials.ConsumerKey) && !String.IsNullOrEmpty(Credentials.ConsumerSecret))
            {
                headers.Add(new OAuthQueryParameter("oauth_consumer_key", StringFormater.UrlEncode(Credentials.ConsumerKey), true, true, false));
                headers.Add(new OAuthQueryParameter("oauth_consumer_secret", StringFormater.UrlEncode(Credentials.ConsumerSecret), false, false, true));
            }

            // Add Header for authenticated connection to a Twitter Application
            if (!String.IsNullOrEmpty(Credentials.AccessToken) && !String.IsNullOrEmpty(Credentials.AccessTokenSecret))
            {
                headers.Add(new OAuthQueryParameter("oauth_token", StringFormater.UrlEncode(Credentials.AccessToken), true, true, false));
                headers.Add(new OAuthQueryParameter("oauth_token_secret", StringFormater.UrlEncode(Credentials.AccessTokenSecret), false, false, true));
            }
            else
            {
                headers.Add(new OAuthQueryParameter("oauth_token", "", false, false, true));
            }

            return headers;
        }

        // Retrieve a HttpWebRequest for a specific query
        public virtual HttpWebRequest GetQueryWebRequest(
            string url,
            HttpMethod httpMethod,
            IEnumerable<IOAuthQueryParameter> headers = null)
        {
            if (headers == null)
            {
                headers = GenerateParameters();
            }

            return _queryGenerator.GenerateWebRequest(url, httpMethod, headers);
        }

        public virtual string ExecuteQueryWithSpecificParameters(
            string url,
            HttpMethod httpMethod,
            WebExceptionHandlingDelegate exceptionHandler,
            IEnumerable<IOAuthQueryParameter> headers)
        {
            string result = null;

            HttpWebRequest httpWebRequest = null;
            WebResponse response = null;

            try
            {
                httpWebRequest = GetQueryWebRequest(url, httpMethod, headers);
                httpWebRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);

                // Opening the connection
                response = httpWebRequest.GetResponse();
                System.IO.Stream stream = response.GetResponseStream();

                _lastHeadersResult = response.Headers;

                if (stream != null)
                {
                    // Getting the result
                    StreamReader responseReader = new StreamReader(stream);
                    result = responseReader.ReadLine();
                }

                // Closing the connection
                response.Close();
                httpWebRequest.Abort();
            }
            catch (WebException wex)
            {
                int? statusNumber =  Tweetinvi.Utils.ExceptionUtils.GetWebExceptionStatusNumber(wex);

                if(statusNumber != null)
                {
                    switch (statusNumber)
                    {
                        case 429:
                            //wait 15 minutes then retry the call
                            Console.WriteLine("Rate Limit Reached in GetUserTimeline, Sleeping for 15 minutes...");
                            System.Threading.Thread.Sleep(900000);
                            Console.WriteLine("Woke up...");

                            httpWebRequest = GetQueryWebRequest(url, httpMethod, headers);
                            httpWebRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);

                            // Opening the connection
                            response = httpWebRequest.GetResponse();
                            System.IO.Stream stream = response.GetResponseStream();

                            _lastHeadersResult = response.Headers;

                            if (stream != null)
                            {
                                // Getting the result
                                StreamReader responseReader = new StreamReader(stream);
                                result = responseReader.ReadLine();
                            }

                            // Closing the connection
                            response.Close();
                            httpWebRequest.Abort();
                            break;
                        case 401:
                            Console.WriteLine(String.Format("{0}: Unauthorized operation for user : {1}...", DateTime.Now, url));
                            break;
                        case 403: //Forbidden
                            Console.WriteLine(String.Format("{0}: Twitter server refused or access is not allowed : {1},\nData will not be retrieved for: {2}", DateTime.Now, wex.Message.ToString(), url));
                            break;
                        case 404: //Not found
                            Console.WriteLine(String.Format("{0}: The URI requested is invalid or the resource requested, such as a user, does not exists : {1},\nData will not be retrieved for: {2}", DateTime.Now, wex.Message.ToString(), url));
                            break;
                        case 406: //Not acceptable
                            Console.WriteLine(String.Format("{0}: Invalid format specified in the request. : {1},\nData will not be retrieved for: {2}", DateTime.Now, wex.Message.ToString(), url));
                            break;
                        case 410: //Gone
                            Console.WriteLine(String.Format("{0}: This resource is gone, requests to this endpoint will yield no results from now on. : {1},\nData will not be retrieved for: {2}", DateTime.Now, wex.Message.ToString(), url));
                            break;
                        case 500:
                            Console.WriteLine(String.Format("{0}: Twitter server returned Internal Server Error : {1},\nData will not be retrieved for: {2}", DateTime.Now, wex.Message.ToString(), url));
                            break;
                        case 502: //Bad gateway - Twitter is down
                            Console.WriteLine(String.Format("{0}: Twitter servers are down or being upgraded: {1},\nData will not be retrieved for: {2}", DateTime.Now, wex.Message.ToString(), url));
                            throw;
                            break;
                        case 503: //Service unavailable
                            Console.WriteLine(String.Format("{0}: Twitter servers are up but overloaded with requests : {1},\nData will not be retrieved for: {2}", DateTime.Now, wex.Message.ToString(), url));
                            System.Threading.Thread.Sleep(1000);

                            httpWebRequest = GetQueryWebRequest(url, httpMethod, headers);
                            httpWebRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);

                            // Opening the connection
                            response = httpWebRequest.GetResponse();
                            stream = response.GetResponseStream();

                            _lastHeadersResult = response.Headers;

                            if (stream != null)
                            {
                                // Getting the result
                                StreamReader responseReader = new StreamReader(stream);
                                result = responseReader.ReadLine();
                            }

                            // Closing the connection
                            response.Close();
                            httpWebRequest.Abort();
                            //throw;
                            break;
                        case 504: //Gateway timeout
                            Console.WriteLine(String.Format("{0}: The Twitter servers are up, but the request couldn't be serviced due to some failure : {1},\nData will not be retrieved for: {2}", DateTime.Now, wex.Message.ToString(), url));
                            System.Threading.Thread.Sleep(1000);

                            httpWebRequest = GetQueryWebRequest(url, httpMethod, headers);
                            httpWebRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);

                            // Opening the connection
                            response = httpWebRequest.GetResponse();
                            stream = response.GetResponseStream();

                            _lastHeadersResult = response.Headers;

                            if (stream != null)
                            {
                                // Getting the result
                                StreamReader responseReader = new StreamReader(stream);
                                result = responseReader.ReadLine();
                            }

                            // Closing the connection
                            response.Close();
                            httpWebRequest.Abort();
                            //throw;
                            break;
                        default:
                            if (exceptionHandler != null)
                            {
                                exceptionHandler(wex);
                            }
                            else
                            {
                                throw;
                            }

                            if (response != null)
                            {
                                response.Close();
                            }

                            if (httpWebRequest != null)
                            {
                                httpWebRequest.Abort();
                            }
                            break;
                    }
                }
            }

            return result;
        }

        // Execute a generic simple query
        public virtual string ExecuteQuery(
            string url,
            HttpMethod httpMethod,
            WebExceptionHandlingDelegate exceptionHandler)
        {
            return ExecuteQueryWithSpecificParameters(url, httpMethod, exceptionHandler, GenerateParameters());
        }

        #endregion
    }
}
