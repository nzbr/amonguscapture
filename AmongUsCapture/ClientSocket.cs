using System;
using System.Text.Json;
using SocketIOClient;
using System.Drawing;
using MetroFramework;
using TextColorLibrary;

namespace AmongUsCapture
{
    public class ClientSocket
    { 
        public event EventHandler OnConnected;
        public event EventHandler OnDisconnected;

        private SocketIO socket;
        private string ConnectCode;

        public void Init()
        {
            // Initialize a socket.io connection.
            socket = new SocketIO();

            // Handle tokens from protocol links.
            IPCadapter.getInstance().OnToken += OnTokenHandler;

            // Register handlers for game-state change events.
            GameMemReader.getInstance().GameStateChanged += GameStateChangedHandler;
            GameMemReader.getInstance().PlayerChanged += PlayerChangedHandler;
            GameMemReader.getInstance().JoinedLobby += JoinedLobbyHandler;

            // Handle socket connection events.
            socket.OnConnected += (sender, e) =>
            {
                // Report the connection
                Settings.form.setColor(MetroColorStyle.Green);
                Settings.conInterface.WriteModuleTextColored("ClientSocket", Color.Cyan, "Connected successfully!");


                // Alert any listeners that the connection has occurred.
                OnConnected?.Invoke(this, EventArgs.Empty);

                // On each (re)connection, send the connect code and then force-update everything.
                socket.EmitAsync("connectCode", ConnectCode).ContinueWith((_) =>
                {
                    Settings.conInterface.WriteModuleTextColored("ClientSocket", Color.Cyan, $"Connection code ({Color.Red.ToTextColor()}{ConnectCode}{UserForm.NormalTextColor.ToTextColor()}) sent to server.");
                    GameMemReader.getInstance().ForceUpdatePlayers();
                    GameMemReader.getInstance().ForceTransmitState();
                    GameMemReader.getInstance().ForceTransmitLobby();
                });
            };

            // Handle socket disconnection events.
            socket.OnDisconnected += (sender, e) =>
            {
                // Report the disconnection.
                Settings.form.setColor(MetroColorStyle.Red);
                Settings.conInterface.WriteModuleTextColored("ClientSocket", Color.Cyan, $"{Color.Red.ToTextColor()}Connection lost!");

                // Alert any listeners that the disconnection has occured.
                OnDisconnected?.Invoke(this, EventArgs.Empty);
            };
        }

        public void OnTokenHandler(object sender, StartToken token)
        {
            if (socket.Connected)
            {
                // Disconnect from the existing host...
                socket.DisconnectAsync().ContinueWith((t) =>
                {
                    // ...then connect to the new one.
                    this.Connect(token.Host, token.ConnectCode);
                });
            } else
            {
                // Connect using the host and connect code specified by the token.
                this.Connect(token.Host, token.ConnectCode);
            }
        }

        private void OnConnectionFailure(AggregateException e = null)
        {
            string message = e != null ? e.Message : "A generic connection error occured.";
            Settings.conInterface.WriteModuleTextColored("ClientSocket", Color.Cyan, $"{Color.Red.ToTextColor()}{message}");
        }

        private void Connect(string url, string connectCode)
        {
            try
            {
                ConnectCode = connectCode;
                socket.ServerUri = new Uri(url);
                socket.ConnectAsync().ContinueWith(t =>
                {
                    if (!t.IsCompletedSuccessfully)
                    {
                        OnConnectionFailure(t.Exception);
                        return;
                    }
                });
            } catch (ArgumentNullException) {
                Console.WriteLine("Invalid bot host, not connecting");
            } catch (UriFormatException) {
                Console.WriteLine("Invalid bot host, not connecting");
            }
        }

        private void GameStateChangedHandler(object sender, GameStateChangedEventArgs e)
        {
            if (!socket.Connected) return;
            socket.EmitAsync("state", JsonSerializer.Serialize(e.NewState)); // could possibly use continueWith() w/ callback if result is needed
        }

        private void PlayerChangedHandler(object sender, PlayerChangedEventArgs e)
        {
            if (!socket.Connected) return;
            socket.EmitAsync("player", JsonSerializer.Serialize(e)); //Makes code wait for socket to emit before closing thread.
        }

        private void JoinedLobbyHandler(object sender, LobbyEventArgs e)
        {
            if (!socket.Connected) return;
            socket.EmitAsync("lobby", JsonSerializer.Serialize(e));
            Settings.conInterface.WriteModuleTextColored("ClientSocket", Color.Cyan,
                $"Room code ({Color.Yellow.ToTextColor()}{e.LobbyCode}{UserForm.NormalTextColor.ToTextColor()}) sent to server.");
        }
    }
}
