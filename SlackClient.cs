﻿using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using SlackNet.Bot;

namespace DibbrBot;

/// <summary>
///     A client to slack
/// </summary>
class SlackChat : ChatSystem
{
    public MessageRecievedCallback Callback;
    public string ChatLog = "";

    public SlackChat() => ChatLog = "";


    public override void Typing(bool start)
    {
        // API.Typing(client, channel, start);
    }


    // Initialize slack client
    public override async Task Initialize(MessageRecievedCallback callback, string token = null)
    {
        Callback = callback;
        var s = new Thread(async () =>
        {
            var bot = new SlackBot(token ?? ConfigurationManager.AppSettings["SlackBotApiToken"]);

            //bot.Responders.Add(x => !x.BotHasResponded, rc => "My responses are limited, you must ask the right question...");
            // .NET events
            bot.OnMessage += (sender, message) =>
            {
                var txt = message.Text;

                // Change @bot to bot, query
                if (message.MentionsBot && !txt.ToLower().StartsWith(Program.BotName))
                    txt = Program.BotName + " " + txt[(txt.IndexOf(">") + 1)..];

                ChatLog += message.User.Name + ": " + message.Text + Program.NewLogLine;

                if (txt.ToLower().StartsWith(Program.BotName))
                {
                    message.ReplyWith(async () =>
                    {
                        var (_, msg) = await callback(txt, message.User.Name, true);
                        if (msg == null) return null;

                        ChatLog += Program.BotName + ": " + msg + Program.NewLogLine;
                        return new() {Text = msg};
                    });
                }
                /* handle message */
            };
            //  bot.Messages.Subscribe(( message) => {
            //     Console.WriteLine(message.Text);
            // });
            //  bot.AddHandler(new MyMessageHandler(this));

            try
            {
                await bot.Connect();
                await Task.Delay(200);
            }
            catch (Exception e) { Console.WriteLine(e.Message); }

            while (true) { }
        });
        s.Start();
    }

    /*   class MyMessageHandler : IMessageHandler
       {
           public MyMessageHandler(SlackChat chat)
           {
               slack = chat;
           }
           public static SlackChat slack;

           public Task HandleMessage(IMessage message)
           {
               slack.ChatLog += message.User.Name + ": " + message.Text + "\n";
               message.ReplyWith(async () => {
                   var msg = await slack.Callback(message.Text, message.User.Name);
                   return new BotMessage { Text = msg };
               });
               return Task.FromResult(0);
           }
       }*/
    void Bot_MessageReceived(string messageText) { Console.WriteLine("Msg = " + messageText); }
}