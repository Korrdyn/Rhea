using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rhea.Services;
using Victoria.Node;

namespace Rhea;

public class Program
{
    private readonly IServiceProvider serviceProvider;

    private Program()
    {
        serviceProvider = CreateProvider();
    }

    static void Main()
        => new Program().RunAsync().GetAwaiter().GetResult();

    static IServiceProvider CreateProvider()
    {
        var collection = new ServiceCollection()
            .AddSingleton(new LoggerFactory())
            .AddSingleton(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.GuildVoiceStates | GatewayIntents.GuildMembers | GatewayIntents.Guilds
            })
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton(new InteractionServiceConfig())
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
            .AddSingleton(x => new LavaNode(x.GetRequiredService<DiscordSocketClient>(), new NodeConfiguration
            {
                Authorization = Environment.GetEnvironmentVariable("LAVALINK_AUTH"),
            }, x.GetRequiredService<LoggerFactory>().CreateLogger<LavaNode>()))
            .AddSingleton(x => new AudioService(x.GetRequiredService<LavaNode>()));

        return collection.BuildServiceProvider();
    }

    async Task RunAsync()
    {
        var client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        var handler = serviceProvider.GetRequiredService<InteractionService>();
        var lavalink = serviceProvider.GetRequiredService<LavaNode>();

        await handler.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);

        client.Log += async (msg) =>
        {
            Console.WriteLine(msg);
            await Task.CompletedTask;
        };

        handler.Log += async (msg) =>
        {
            Console.WriteLine(msg);
            await Task.CompletedTask;
        };

        client.Ready += async () =>
        {
            await lavalink.ConnectAsync();
            if (IsDebug())
                await handler.RegisterCommandsToGuildAsync(918704583717572639);
            else
                await handler.RegisterCommandsGloballyAsync();
        };

        client.InteractionCreated += async (interaction) =>
        {
            try
            {
                var context = new SocketInteractionContext(client, interaction);

                await handler.ExecuteCommandAsync(context, serviceProvider);
            }
            catch
            {
                if (interaction.Type is InteractionType.ApplicationCommand)
                    await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
            }
        };

        handler.SlashCommandExecuted += SlashCommandExecuted;


        await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("TOKEN"));
        await client.StartAsync();

        await Task.Delay(Timeout.Infinite);
    }

    private static async Task SlashCommandExecuted(SlashCommandInfo command, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            Console.WriteLine($"{context.User} tried to run {command.Name} but ran into {result.Error.ToString()}");
            var embed = new EmbedBuilder
            {
                Color = new Color(0x2F3136),
            };
            switch (result.Error)
            {
                case InteractionCommandError.BadArgs:
                    embed.Title = "Invalid Arguments";
                    embed.Description =
                        "Please make sure the arguments you're providing are correct.\nIf you keep running into this message, please join the support server";
                    break;
                case InteractionCommandError.ConvertFailed:
                case InteractionCommandError.Exception:
                    embed.Title = "Error Occurred";
                    embed.Description = "I ran into a problem running your command.\nIf it continues to happen join the support server";
                    break;
                case InteractionCommandError.Unsuccessful:
                    embed.Title = "Something Happened ";
                    embed.Description = "I was unable to run your command.\nIf it continues to happen join the support server";
                    break;
            }

            if (context.Interaction.HasResponded)
            {
                await context.Interaction.ModifyOriginalResponseAsync(m => m.Embed = embed.Build());
            }
            else await context.Interaction.RespondAsync(embed: embed.Build(), ephemeral: true);
        }
        else
        {
            var guild = context.Interaction.GuildId == null
                ? "DM"
                : $"{context.Guild.Name} ({context.Guild.Id}) #{context.Channel.Name} ({context.Channel.Id})";
            Console.WriteLine(
                $"{guild} {context.User.Username}#{context.User.Discriminator} ({context.User.Id}) ran /{command.Name} {string.Join(",", ((SocketSlashCommandData)context.Interaction.Data).Options.Select(o => $"{o.Name}:{o.Value}"))}");
        }
    }

    private static bool IsDebug()
    {
#if DEBUG
        return true;
#else
            return false;
#endif
    }
}