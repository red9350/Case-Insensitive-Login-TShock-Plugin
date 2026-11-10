using System;
using System.Collections.Generic;
using System.Data;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace CaseInsensitiveLoginPlugin
{
    [ApiVersion(2, 1)]
    public class CaseInsensitiveLogin : TerrariaPlugin
    {
        public override string Name => "Case Insensitive Login & Userlist";
        public override string Author => "red9350";
        public override string Description => "Forces /login to treat usernames case-insensitively and provides an owner-only /userlist command.";
        public override Version Version => new Version(1, 1, 0);

        private Command? originalLoginCommand;

        public CaseInsensitiveLogin(Main game) : base(game) { }

        public override void Initialize()
        {
            // Extract TShock's native login command logic before we clear it
            foreach (var cmd in Commands.ChatCommands)
            {
                if (cmd.HasAlias("login"))
                {
                    originalLoginCommand = cmd;
                    break;
                }
            }

            // Remove it from the public chat hook list
            Commands.ChatCommands.RemoveAll(c => c.HasAlias("login"));
            
            // Register our customized interception proxy handler
            Commands.ChatCommands.Add(new Command("tshock.account.login", CustomLoginHandler, "login"));

            // Register the Owner/Console exclusive userlist command
            // We use the "tshock.superadmin" permission node to restrict this completely to owners/console
            Commands.ChatCommands.Add(new Command("tshock.superadmin", UserListHandler, "userlist"));
        }

        private void CustomLoginHandler(CommandArgs args)
        {
            if (args.Parameters.Count < 2)
            {
                originalLoginCommand?.CommandDelegate(args);
                return;
            }

            string typedUsername = args.Parameters[0];
            string typedPassword = args.Parameters[1];
            string? correctCasedUsername = null;

            try
            {
                using (QueryResult result = TShock.DB.QueryReader("SELECT Username FROM Users WHERE LOWER(Username) = @0", typedUsername.ToLower()))
                {
                    if (result.Read())
                    {
                        correctCasedUsername = result.Get<string>("Username");
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[CaseInsensitiveLogin] Database lookup failure: {ex.Message}");
            }

            if (string.IsNullOrEmpty(correctCasedUsername))
            {
                originalLoginCommand?.CommandDelegate(args);
                return;
            }

            args.Parameters[0] = correctCasedUsername;

            if (originalLoginCommand != null)
            {
                originalLoginCommand.CommandDelegate(args);
            }
            else
            {
                args.Player.SendErrorMessage("Internal server error: Native login handler could not be located.");
            }
        }

        private void UserListHandler(CommandArgs args)
        {
            List<string> users = new List<string>();

            // Query the active user database to pull all registered names
            try
            {
                using (QueryResult result = TShock.DB.QueryReader("SELECT Username FROM Users ORDER BY Username ASC"))
                {
                    while (result.Read())
                    {
                        string? name = result.Get<string>("Username");
                        if (!string.IsNullOrEmpty(name))
                        {
                            users.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                args.Player.SendErrorMessage($"Failed to fetch database user list: {ex.Message}");
                return;
            }

            if (users.Count == 0)
            {
                args.Player.SendInfoMessage("There are no registered users present in the database.");
                return;
            }

            // Cleanly format and present the list back to the Owner or Console
            args.Player.SendSuccessMessage($"Found {users.Count} registered user(s) in the database:");
            args.Player.SendInfoMessage(string.Join(", ", users));
        }
    }
}
