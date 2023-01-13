using Victoria.Node;
using Victoria.Node.EventArgs;
using Victoria.Player;

namespace Rhea.Services;

public enum LoopType
{
    Single,
    Queue
}

public class AudioService
{
    private readonly LavaNode lavalink;
    public readonly Dictionary<ulong, LoopType> playerLoop = new();

    public AudioService(LavaNode lavalink)
    {
        this.lavalink = lavalink;
        this.lavalink.OnTrackEnd += OnTrackEndAsync;
        this.lavalink.OnTrackStuck += OnTrackStuckAsync;
        this.lavalink.OnTrackException += OnTrackExceptionAsync;
        this.lavalink.OnWebSocketClosed += OnWebSocketClosedAsync;
    }

    private static async Task OnTrackExceptionAsync(TrackExceptionEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
    {
        if (arg.Player.Vueue.Count != 0) await arg.Player.SkipAsync();
        await arg.Player.TextChannel.SendMessageAsync("Player got stuck, skipping to next song.");
    }

    private static async Task OnTrackStuckAsync(TrackStuckEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
    {
        if (arg.Player.Vueue.Count != 0) await arg.Player.SkipAsync();
        await arg.Player.TextChannel.SendMessageAsync("Player got stuck, skipping to next song.");
    }

    private async Task OnWebSocketClosedAsync(WebSocketClosedEventArg arg)
    {
        await lavalink.ConnectAsync();
    }

    private async Task OnTrackEndAsync(TrackEndEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
    {
        switch ((LoopType?)(playerLoop.TryGetValue(arg.Player.VoiceChannel.GuildId, out var loop) ? loop : null))
        {
            case LoopType.Single:
                await arg.Player.PlayAsync(arg.Track);
                break;
            case LoopType.Queue:
            {
                arg.Player.Vueue.Enqueue(arg.Track);
                arg.Player.Vueue.TryDequeue(out var track);
                await arg.Player.PlayAsync(track);
                break;
            }
            default:
            {
                if (arg.Player.Vueue.Count == 0) return;

                arg.Player.Vueue.TryDequeue(out var track);
                await arg.Player.PlayAsync(track);
                break;
            }
        }
    }
}