﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using ServiceStack;
using ServiceStack.Text;

//using DSharpPlus.CommandsNext;

namespace DibbrBot;

/// <summary>
///     Discord chat system
///     TODO: This will replace the messy code below, like this
///     var discord = new DiscordChat(true,true,"channelid");
///     discord.Initialize(GPTHandleMessage);
/// </summary>
public class DiscordChat : ChatSystem
{
    public HttpClient _client;
    public readonly static string mrgirl = "937151566266384394";
    public readonly static string davinci_beta = "983124805995880478";
    public readonly static string davinci = "983124621127725076";

    public string BotUsername, BotName;

    //private string ChatLog = "";
    // private readonly int MAX_BUFFER = 5000; // Chat buffer, don't change this, change the one in GPT3
    public List<Channel> Channels = new();
    public bool IsBot;

    public DiscordChat(bool isBot, string BotName, string BotUsername)
    {
        this.BotUsername = this.BotName = BotName;
        if (BotUsername != null) BotUsername = BotUsername;
    }


    public override void Typing(bool start)
    {
        // API.Typing(client, channel, start);
    }

    public void AddChannel(string channel, bool dm, MessageHandler handler, bool sayHi = false)
    {
        var c = new Channel {Id = channel, Dm = dm};
        c.Handler = handler;

        handler.BotName = BotName;
        Channels.Add(c);
        if (c.Dm) handler.ChattyMode = false;

        // Bot is not allowed here
        if (c.Id == "572387680235683861")
        {
            //H  handler.muted = true;
            handler.ChattyMode = false;
        }
        else { handler.SayHelo = sayHi; }

        if (c.Id == davinci) { handler.Gpt3 = new Gpt3(handler.Gpt3._token, "davinci"); }

        if (c.Id == davinci_beta) { handler.Gpt3 = new Gpt3(handler.Gpt3._token, "text-davinci-001"); }

        /// dibbr pro, better memory
        if (c.Id == "979230826346709032") handler.MessageHistory *= 3;
        Console.WriteLine("Joined " + c.Id);
    }

    public override async Task Initialize(MessageRecievedCallback callback, string token = null)
    {
        if (_client == null)
        {
            _client = new();
            _client.Timeout = TimeSpan.FromSeconds(10);
            // set the headers
            _client.DefaultRequestHeaders.Add("Accept", "*/*");
            _client.DefaultRequestHeaders.Add("Accept-Language", "en-US");
            _client.DefaultRequestHeaders.Add("Authorization", token);
            _client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) discord/0.0.309 Chrome/83.0.4103.122 Electron/9.3.5 Safari/537.36");

            // SSL Certificate Bypass
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            // 2 secs for headers to take
            //  await Task.Delay(1000);
        }

        new Thread(async delegate()
        {
            while (true)
            {
                //
                // Parse new DMs - a hack because I dunno how to get the discord user id
                //
                var hack = ConfigurationManager.AppSettings["hack"];
                if (hack is "true")
                {
                    var dms = await Api.GetNewDMs(_client,
                        "884911258413989978"); // <--- Replace with discordapi.getuserid()
                    if (dms != null)
                        foreach (var dm in dms)
                        {
                            var id = dm["recipients"][0]["id"].ToString();
                            var found = Channels.Where(c => c.Id == id).Count();
                            if (found == 0 && id != "948792559017283659") // Don't add the discord bot
                            {
                                var gpt3 = new Gpt3(ConfigurationManager.AppSettings["OpenAI"], "text-davinci-002");
                                //  AddChannel(id, true, new(this, gpt3));
                            }
                        }
                }

                try
                {
                    var threads = new List<Task>();
                    foreach (var channel in Channels)
                    {
                        //    if (channel.NextCheck > DateTime.Now || channel.Id == "336772169893937154")
                        await channel.Update(this);
                        //lse { Console.WriteLine($"Skipping {channel.Id}"); }


                        await Task.Delay(200);
                    }

                    Task.WaitAll(threads.ToArray());

                    ///while (threads.Any(t => t.IsAlive)) { Thread.Sleep(1000); }
                }
                catch (Exception e)
                {
                    if (e.GetType() == typeof(System.InvalidOperationException))
                        // We added a channel, ignore
                        continue;
                }
            }
        }).Start();
    }

    bool backMsg = false;

    public class Channel
    {
        public bool Dm;
        public int FailCount = 0;
        public int SuccessCount = 0;
        public bool First = true;
        public MessageHandler Handler;
        public string Id;
        public string Log = "";
        public string Name;
        public Dictionary<string, Message> Messages = new Dictionary<string, Message>();
        public DateTime LastMsg = DateTime.MinValue;
        public int Ticks = 0;
        public DateTime NextCheck = DateTime.Now.AddSeconds(60);

        public async Task Update(DiscordChat client)
        {
            var channel = Id;
            //  await Task.Delay(1000);

            //   if(NextCheck < Date)
            // Rarely update sorta dead channels
            var mins = (DateTime.Now - LastMsg).TotalMinutes;
            if (mins > 20)
            {
                Ticks++;
                /*  if (Ticks >= 2)
                  {
                      if (mins < 50 && Ticks < 2) return;
                      if (mins < 150 && Ticks < 5) return;
                      if (mins < 250 && Ticks < 15) return;
                      if (mins < 600 && Ticks < 100) return;
                      if (mins < 1600 && Ticks < 200) return;
                      if (mins < 21600 && Ticks < 500) return;
                  }*/
            }

            bool Contains(List<string> messages, string str)
            {
                str = str.Remove(Program.NewLogLine).Trim();
                foreach (var l in messages)
                {
                    if (l.Remove(Program.NewLogLine).Contains(str)) return true;
                }

                return false;
            }

            // await Task.Delay(100);
            // Read message
            // TODO: Use getLatestMessages()
            // var message = dm ? await API.getLatestdm(client, channel) : await API.getLatestMessage(client, channel);
            var messages = Dm ? await Api.GetLatestDMs(client._client, channel)
                : await Api.GetLatestMessages(client._client, channel, numMessages: SuccessCount == 0 ? 25 : 5);
            if (messages == null)
            {
                await Task.Delay(300);
                FailCount++;
                return;
            }

            SuccessCount++;
            var newMessages = new List<Message>();
            // Skip messages already replied to
            for (var i = messages.Count - 1; i >= 0; i--)
            {
                var msg = messages[i];
                if (!Messages.ContainsKey(msg.Id.ToString())) { Messages.Add(msg.Id.ToString(), msg); }

                else { continue; }

                Log += " " + msg.Author.Username + ": " + msg.Content + Program.NewLogLine;
                if (msg.Timestamp > LastMsg) LastMsg = msg.Timestamp;

                // Skip older messages, or if bot restarts he may spam
                if (msg.Timestamp.AddSeconds(60 * 60) < DateTime.Now)
                {
                    msg.Flags = 69;
                    //  Console.WriteLine($"Skipped old message: {auth}: " + msg);
                    continue;
                }


                // Most recent DM is from bot, so all other messages are replied to
                if (Dm && msg.Author.Username == client.BotUsername)
                {
                    // Set flags to ignore all, but don't remove message, as we might want to add to log
                    for (var k = i; k < messages.Count; k++) messages[k].Flags = 69;

                    break;
                }

                newMessages.Add(msg);
                var user = msg.ReferencedMessage?.Author.ToString();
                var id = msg.ReferencedMessage?.Id.ToString();
                if (msg.Mentions?.Count > 0)
                {
                    user = msg.Mentions?[0]?.Username.ToString();
                    id = msg.Mentions?[0]?.Id.ToString();
                    msg.ReferencedMessage = new ReferencedMessage {Author = new Author {Username = user}, Id = id};
                }

                if (user != null && user != client.BotName)
                {
                    // Ignore, was for someone else
                    msg.Flags = 69;
                }

                // If this is not a bot reply message, ignore
                // continue;

                // Bot reply, so flag the replied message as replied to
                foreach (var msg2 in
                         from msg2 in messages where (msg2.Id.ToString() == id ||
                                                      (msg2.Author.Username == user && msg2.Timestamp < msg.Timestamp))
                         select msg2)
                {
                    msg2.ReplyScore++;
                    if (msg.Author.Username == client.BotUsername && id != null)
                    {
                        msg2.Flags = 69; // skip
                        // msg.Flags = 69; // skip this too
                    }
                }
            }

            // var log = Log.TakeLastLines(messages.Count * 2); // assume extra replies
            for (var i = newMessages.Count - 1; i >= 0; i--)
            {
                var message = newMessages[i];
                var msgid = message.Id;
                var msg = message.Content;
                var auth = message.Author.Username.Replace("????", "Q");
                string editMsg = null;

                if (msg.StartsWith("!!")) editMsg = msg.After("!! ");

                // 1. Wipe memory
                if (msg.Contains("wipe memory")) { Log = ""; }

                // So GPT-3 doesn't get confused, in case these differ
                if (auth == client.BotUsername) auth = client.BotName;

                var c = auth + ": " + msg + Program.NewLogLine;


                if (message.Flags == 69) continue;

                var isForBot = false;

                // Is for bot if it's an edit message
                isForBot |= editMsg != null && auth == client.BotUsername;

                // dibbr demo channel
                //isForBot |= ((msg.Contains("?") || msg.ToLower().Contains(Program.BotName)));

                // Is for bot if it's a reply to the bot
                isForBot |= (message.ReferencedMessage?.Author.Username == client.BotName && msg.Length > 5);
                // or DM
                isForBot |= Dm;

                // If you wanna log context, too
                // if(lastMsgTime == DateTime.MinValue || DateTime.Now-lastMsgTime < DateTime.FromSeconds(15))
                //  File.AppendAllText("chat_log_" + channel + ".txt", c);

                if (isForBot || msg.ToLower().Contains(Program.BotName)) Api.Typing(client._client, channel, true);

                string reply = "";
                bool isReply1 = false;
                if (Dm && auth != "dabbr")
                    reply = "DMs currently not enabled unless you send dabbr money";
                else
                {
                    //var t = new Thread(async () =>
                    //{
                    Handler.Log = Log;
                    (isReply1, reply) = await Handler.OnMessage(msg, auth, isForBot,
                        message.ReferencedMessage?.Author.Username == client.BotName);

                    if (StringExtensions.IsNullOrEmpty(reply)) continue;
                }


                _ = Dm ? await Api.send_dm(client._client, channel, reply, msgid)
                    : await Api.send_message(client._client, channel, reply, isReply1 ? msgid : null, editMsg);

                NextCheck = DateTime.Now.AddSeconds(1000);
                // Write out our response, along with the question
                File.AppendAllTextAsync("chat_log_" + channel + ".txt",
                    c + client.BotName + ": " + reply + Program.NewLogLine);
                //  });
                //  t.Start();
            }


            await Handler.OnUpdate(async s =>
            {
                // FIXME: Need generic handler for all client types
                _ = Dm ? await Api.send_dm(client._client, channel, s, null)
                    : await Api.send_message(client._client, channel, s, null);
            });
        }
    }
}

/// <summary>
///     API Client based discord access. More features than DiscordV1, notably you can use with bots
///     Doesn't support selfbots
/// </summary>
class DiscordChatV2 : ChatSystem, IDisposable
{
    readonly bool _disposed = false;
    readonly int _maxBuffer = 1000; // Chat buffer to GPT3 (memory) THIS CAN GET EXPENSIVE
    public MessageHandler handler;
    DiscordClient _discordClient;
    MessageRecievedCallback _callback;
    public string ChatLog = "";
    public string Id = "";

    string _lastMsg = "";


    /* DISPOSE OF MANAGED RESOURCES */
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public override void Typing(bool start)
    {
        //  API.Typing(client, channel, start);
    }

    public override async Task Initialize(MessageRecievedCallback callback, string token)
    {
        bool bot = false;
        if (token.StartsWith("BOT"))
        {
            token = token.After("BOT ");
            bot = true;
        }

        Id = token;
        _callback = callback;
        try
        {
            var clientConfig = new DiscordConfiguration
            {
                //  MinimumLogLevel = LogLevel.Critical,
                Token = token, TokenType = TokenType.Bot
                //  UseInternalLogHandler = true
            };

            /* INSTANTIATE DISCORDCLIENT */
            _discordClient = new(clientConfig);
            _discordClient.MessageCreated += MessageCreated;

            await _discordClient.ConnectAsync();
            Console.WriteLine("V 2.0 Discord Bot Client Online");
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Thread.Sleep(5000);
        }

        await Task.Delay(-1);
    }

    /// <summary>
    ///     Me
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    Task<Task> MessageCreated(DiscordClient sender, MessageCreateEventArgs e)
    {
        new Thread(async delegate()
        {
            var c = e.Author.Username + ": " + e.Message.Content + "\n";
            ChatLog += c;
            if (ChatLog.Length > _maxBuffer) ChatLog = ChatLog[^_maxBuffer..];
            handler.Log = ChatLog;
            var first = _lastMsg == "";
            if (c == _lastMsg) return;

            _lastMsg = c;
            if (first) return;

            Console.WriteLine(c);
            File.AppendAllText("chat_log_" + e.Channel.Name + ".txt", c);


            var (_, str) = await _callback(e.Message.Content, e.Author.Username);

            var msgs = new List<string>();
            if (str.Length > 2000)
            {
                msgs.Add(str[..1999]);
                msgs.Add(str[^1999..]);
            }
            else
                msgs.Add(str);

            foreach (var msg in msgs) await sender.SendMessageAsync(e.Channel, msg);
        }).Start();
        return Task.FromResult(Task.CompletedTask);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing) Initialize(null, null).Dispose();
    }
}

public static class StringHelp
{
    public static string Rem(this string input, string search, string replacement = "")
    {
        var result = Regex.Replace(input, Regex.Escape(search), replacement.Replace("$", "$$"),
            RegexOptions.IgnoreCase);
        return result;
    }

    public static string Remove(this string str, string s) =>
        Regex.Replace(str, Regex.Escape(s), "", RegexOptions.IgnoreCase);

    public static List<string> TakeLastLines(this string text, int count)
    {
        var lines = Enumerable.ToArray(text.Split(Program.NewLogLine));
        if (lines.Last().Trim() == "") lines = lines[..^1];

        if (lines.Length > count) return lines[^count..].ToList();

        return lines.ToList();
    }
}