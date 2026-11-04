namespace TiltMaze.App.Services;

// Sender rekorder fra køen (PendingRun) til serveren. Kaldes ved start, efter login, efter en ny rekord
// og når nettet vender tilbage.
public class RunQueue
{
    private readonly GameDatabase database;
    private readonly Api api;
    private readonly Auth auth;
    private readonly Wallet wallet;
    private readonly SemaphoreSlim onlyOne = new(1);

    public RunQueue(GameDatabase database, Api api, Auth auth, Wallet wallet)
    {
        this.database = database;
        this.api = api;
        this.auth = auth;
        this.wallet = wallet;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        // async void: her må der aldrig komme en exception ud, ellers lukker appen
        try
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
                await SendPending();
        }
        catch (Exception)
        {
            // Næste gang der er net, prøver vi igen
        }
    }

    // Ved start: læs det gemte login, og send det der venter
    public async Task Start()
    {
        await auth.Load();
        await SendPending();
    }

    public async Task SendPending()
    {
        if (!auth.IsLoggedIn)
            return;

        await onlyOne.WaitAsync();
        try
        {
            foreach (var run in database.GetPending())
            {
                // Kun gæsterekorder og den indloggede brugers egne sendes
                string owner = run.Username ?? "";
                if (owner != "" && owner != auth.Username)
                    continue;

                var (result, rank) = await api.SendRun(run.Difficulty, run.Seed, run.TimeMs, run.Ghost);
                if (result == SendResult.TryLater)
                    break;   // Resten venter til næste gang
                database.RemovePending(run);   // Sendt, eller afvist for altid
                if (result == SendResult.Sent)
                    wallet.GiveRankBonus(run.Difficulty, run.Seed, rank);
            }
        }
        catch (Exception)
        {
            // Fx en ødelagt række. Køen prøves igen næste gang
        }
        finally
        {
            onlyOne.Release();
        }
    }
}
