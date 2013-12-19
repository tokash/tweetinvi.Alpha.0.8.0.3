using System.Configuration;
using TwitterToken;
using TweetinCore.Interfaces;

namespace Testinvi
{
    public class TokenSingleton
    {
        private static IToken _token;
        private static string _screeName;

        public static void Reset()
        {
            _token = null;
            _screeName = null;
        }

        public static string ScreenName
        {
            get
            {
                if (_screeName == null)
                {
                    _screeName = ConfigurationManager.AppSettings["token_UserScreenName"];
                }

                return _screeName;
            }
        }

        public static IToken Instance
        {
            get
            {
                if (_token == null)
                {
                    _token = new Token(
                        ConfigurationManager.AppSettings["token_AccessToken"],
                        ConfigurationManager.AppSettings["token_AccessTokenSecret"],
                        ConfigurationManager.AppSettings["token_ConsumerKey"],
                        ConfigurationManager.AppSettings["token_ConsumerSecret"]);
                }

                return _token;
            }
        }
    }
}
