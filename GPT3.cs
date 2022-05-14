﻿// (c) github.com/thehemi
// using System;
using OpenAI_API;
using System;
using System.Configuration;
using System.Threading.Tasks;

namespace DibbrBot
{
    /// <summary>
    /// Chat system that uses the OpenAI API to send and receive messages
    /// </summary>
    public class GPT3
    {
        // Shared between all instances
        private static OpenAIAPI api;
        private static  int MAX_CHARS = 1500;

        static string CleanText(string txt)
        {
            if (txt == null) return null;
            txt = txt.Trim();
            txt = txt.Replace("\"", "");
            // Gay stuff GPT-3 likes to return
            if (txt.StartsWith("There is no") || txt.StartsWith("There's no"))
            {
                txt = txt.Substring(txt.IndexOfAny(new char[] { '.', ',' }) + 1);
            }

            // Remove  There's no right or wrong answer blah blah blah at the end
            var last = txt.IndexOf("Ultimately,");
            if (last != -1)
                txt = txt.Substring(0, last);
            return txt;
        }


        static float fp = 0, pp = 0.5f, temp = 1;
        static string engine = "text-davinci-002";
        static Engine e;
        /// <summary>
        /// Asks OpenAI
        /// </summary>
        /// <param name="q"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public static async Task<string> Ask(string q, string user = "")
        {
            if (api == null)
            {
                e= new Engine(engine) { Owner = "openai", Ready = true };
                api = new OpenAI_API.OpenAIAPI(apiKeys: ConfigurationManager.AppSettings["OpenAI"], engine: e);
            }
            
            
            if (q.Length > MAX_CHARS)
                q = q.Substring(q.Length - MAX_CHARS);            
            
            q = q.Trim();

          
            // Prime it with other questions here
            var latestLine = q.LastIndexOf("\n");
            if (latestLine == -1) latestLine = 0;
            var line = q.Substring(latestLine);

            // Set variables like this
            // dibbr hey ?fp=1&pp=2
            if (line.Contains("=") && line.Contains("?"))
            {
                string[] query = line.Split('?');
                if (query.Length == 2)
                {
                    foreach (string pairs in query[1].Split('&'))
                    {
                        string[] values = pairs.Split('=');
                        if (values[0] == "pp")
                            pp = float.Parse(values[1]);
                        if (values[0] == "buffer")
                            MAX_CHARS = int.Parse(values[1]);
                        if (values[0] == "fp")
                            fp = float.Parse(values[1]);
                        if (values[0] == "temp")
                            temp = float.Parse(values[1]);
                        if (values[0] == "engine")
                        {
                            engine = values[1];
                            e = new Engine(engine) { Owner = "openai", Ready = true };
                            api = new OpenAI_API.OpenAIAPI(apiKeys: ConfigurationManager.AppSettings["OpenAI"], engine: e);
                        }
                    }

                    return $"Changes made. Now, fp={fp} pp={pp} temp={temp} engine={engine}";
                }
            }


            string MakeText(string q)
            {
                return ConfigurationManager.AppSettings["PrimeText"] + "\n"
                    + q + "\ndibbr's response: ";
            }
            // Setup context, insert chat history
            var txt = MakeText(q);
            
            string r = await Q(txt,pp,fp,temp);

            // If dup, try again
            var split = r.Split('.');
            int percentMatch = 0;
            foreach (var s in split)
            {
                if (s.Contains(q))
                    percentMatch += s.Length;
            }
            if (percentMatch > q.Length)
                return await Q(MakeText(q), pp, fp, 1);
       
            return r;

            static async Task<string> Q(string txt, float pp, float tp, float temp)
            {
                var result = await api.Completions.CreateCompletionAsync(txt,
                                temperature: temp, top_p: 1,frequencyPenalty:tp,presencePenalty:pp, max_tokens: 1000, stopSequences: new string[] { Program.BotName + ":" });
                // var r = CleanText(result.ToString());
		var r = result.ToString();
                Console.WriteLine("GPT3 response: " + r);
                return r;
            }
        }

        /// <summary>
        /// Asks OpenAI, but as a completion without history
        /// </summary>
        /// <param name="q"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task<string> Ask2(string q, string user = "")
        {
            if (api == null)
            {
                var eng = new Engine("text-davinci-002") { Owner = "openai", Ready = true };
                var k = ConfigurationManager.AppSettings["OpenAI"];
                api = new OpenAI_API.OpenAIAPI(apiKeys: k, engine: eng);
            }
            var stops =
                new string[] { Program.BotName + ":" };
            var result = await api.Completions.CreateCompletionAsync(q,
                temperature: 0.8, top_p: 1, max_tokens: 1000, stopSequences: stops);

            var r = result.ToString();
            Console.WriteLine("GPT3 response: " + r);
            return r;
        }
    }
}
