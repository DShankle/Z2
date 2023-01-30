using RestSharp;
using RestSharp.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Text;


namespace Z2
{

    public class oauthResponse
    {
        public string accessToken { get; set; }
        public string token_type { get; set; }
        public string refresh_token { get; set; }
        public string expires_in { get; set; }
        public string scope { get; set; }
     
    }
    class Auth
    {
        //these will need to be generated with the original beacon
        // not sure if its worth including the original token (currentAt) since the client is likely to be deployed after the hour time limit
        // TOD
        public string currentAt = "";
        private string currentRefreshAt = "";
        private string at = "";
       //end of generated codes
        
        static string accountAuth = "Basic ";
        public oauthResponse dsrResp;


        public void beginAuth()
        {
            
            if (this.refreshToken(currentRefreshAt) == -1)
            {
                
                if (this.authorizeToken(at) == -1)
                {
                    Console.WriteLine("All authentication methods failed.");
                }
                
            }
        }


        public int refreshToken(string token)
        {
            //use refresh token to aquire new api token

            var client = new RestClient($"https://zoom.us/oauth/token?grant_type=refresh_token&refresh_token={token}");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
          
            request.AddHeader("Authorization", accountAuth);
            IRestResponse response = client.Execute(request);
            if ((int)response.StatusCode != 200)
            {
                Console.WriteLine("refresh token response not valid");
                //broken
                return -1;
            }
            //test - this.statusCode = (int)response.StatusCode;
            //need error handling here
            this.dsrResp = new JsonDeserializer().Deserialize<oauthResponse>(response);
            // set token and refresh token after aquiring
            this.currentAt = dsrResp.accessToken;
            this.currentRefreshAt = dsrResp.refresh_token;

            //Console.WriteLine(dsrResp.accessToken);
            //Console.WriteLine(dsrResp.refresh_token);
            
            return 0;

        }

     
        public int authorizeToken(string token)
        {
            //use if both refresh tokens fail
            //this will invalidate the current token/refresh token
            var client = new RestClient($"https://zoom.us/oauth/token?grant_type=authorization_code&code={at}&redirect_uri=https%3A%2F%2Flmgsecurity.com");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Authorization", accountAuth); 
      
            IRestResponse response = client.Execute(request);
            if ((int)response.StatusCode != 200)
            {
                return -1;
            }
            //test - this.statusCode = (int)response.StatusCode;
            //need error handling here
            this.dsrResp = new JsonDeserializer().Deserialize<oauthResponse>(response);
            this.currentAt = dsrResp.accessToken;
            this.currentRefreshAt = dsrResp.refresh_token;

            Console.WriteLine(dsrResp.accessToken);
            Console.WriteLine(dsrResp.refresh_token);
            return 0;
        }

    }
}
