using Discord;
using Discord.Interactions;
using Rhea.Services;
using Victoria;
using Victoria.Node;
using Victoria.Player;
using Victoria.Responses.Search;

namespace Rhea.Modules;

public class AudioModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly LavaNode lavalink;
    private readonly AudioService service;

    public AudioModule(LavaNode lavalink, AudioService service)
    {
        this.lavalink = lavalink;
        this.service = service;
    }

    [SlashCommand("play", "Play some music")]
    public async Task Play(string search)
    {
        var member = Context.Guild.GetUser(Context.User.Id);
        if (member.VoiceState == null)
        {
            await RespondAsync("You must be in a voice channel to run this command.", ephemeral: true);
            return;
        }

        if (!lavalink.TryGetPlayer(Context.Guild, out var player))
        {
            player = await lavalink.JoinAsync(member.VoiceChannel, Context.Channel as ITextChannel);
        }

        if (player.VoiceChannel.Id != member.VoiceChannel.Id)
        {
            await RespondAsync("You must be in the same voice channel as me to run this command.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var searchResponse = await lavalink.SearchAsync(Uri.IsWellFormedUriString(search, UriKind.Absolute) ? SearchType.Direct : SearchType.YouTube, search);

        if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
        {
            await ModifyOriginalResponseAsync(properties => properties.Content = $"Unable to find anything for `{Format.Sanitize(search)}`");
            return;
        }

        if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
        {
            player.Vueue.Enqueue(searchResponse.Tracks);
            var embed = new EmbedBuilder()
                .WithAuthor("Queued Playlist")
                .WithTitle(searchResponse.Playlist.Name)
                .WithUrl(search)
                .AddField("Tracks", searchResponse.Tracks.Count, true)
                .AddField("Playlist length",
                    new TimeSpan(searchResponse.Tracks.Sum(t => t.Duration.Ticks))
                        .ToString(@"hh\:mm\:ss"), true)
                .WithColor(Color.Blue)
                .WithFooter($"{Context.User.Username}#{Context.User.Discriminator}", Context.User.GetAvatarUrl()).Build();

            if (player.PlayerState is not PlayerState.Playing or PlayerState.Paused)
            {
                player.Vueue.TryDequeue(out var track);
                await player.PlayAsync(track);
            }

            await ModifyOriginalResponseAsync(properties => properties.Embed = embed);
        }
        else
        {
            var track = searchResponse.Tracks.First();

            if (player.PlayerState is PlayerState.Playing or PlayerState.Paused)
            {
                var embed = new EmbedBuilder()
                    .WithAuthor("Queued Track")
                    .WithTitle(track.Title)
                    .WithUrl(track.Url)
                    .AddField("Channel", track.Author, true)
                    .AddField("Duration", track.IsStream ? "Stream" : track.Duration.ToString(@"hh\:mm\:ss"), true)
                    .AddField("Time until playing",
                        new TimeSpan(player.Vueue.Sum(t => t.Duration.Ticks) + player.Track.Duration.Ticks - player.Track.Position.Ticks)
                            .ToString(@"hh\:mm\:ss"), true)
                    .AddField("Queue position", player.Vueue.Count + 1)
                    .WithThumbnailUrl(await track.FetchArtworkAsync())
                    .WithColor(Color.Blue)
                    .WithFooter($"{Context.User.Username}#{Context.User.Discriminator}", Context.User.GetAvatarUrl()).Build();

                player.Vueue.Enqueue(track);

                await ModifyOriginalResponseAsync(m => m.Embed = embed);
            }
            else
            {
                var embed = new EmbedBuilder()
                    .WithAuthor("Now Playing")
                    .WithTitle(track.Title)
                    .WithUrl(track.Url)
                    .AddField("Channel", track.Author, true)
                    .AddField("Duration", track.IsStream ? "Stream" : track.Duration.ToString(@"hh\:mm\:ss"), true)
                    .WithThumbnailUrl(await track.FetchArtworkAsync())
                    .WithColor(Color.Green)
                    .WithFooter($"{Context.User.Username}#{Context.User.Discriminator}", Context.User.GetAvatarUrl()).Build();

                await player.PlayAsync(track);
                await ModifyOriginalResponseAsync(properties => properties.Embed = embed);
            }
        }
    }
}