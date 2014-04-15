using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Collections.Generic;

using DwarfKeeper; // Import Client
using DwarfData;
using DwarfCMD;
using Isis;

//FIXME:	There is a bug that if send_message is the first command, 
//			(without any setup) the client crashes with a null pointer exception.

namespace DwarfCLI
{
    delegate string dwarfFun(string[] args);

	public class DwarfCLI
	{
		private static bool isRunning = false;                    //!< Current running status
		private static DwarfClient client;                        //!< Client backend
        private static string prompt = "DwarfKeeper>>";
        private static Dictionary<string, dwarfFun> dwarfFuns;

        private static List<string> noArgCmds = new List<string>() {"help","disconnect", "exit"};

        private static Dictionary<string, string[]> helpStrings =
            new Dictionary<string, string[]>(){
                {"connect", new string[]{"connect <groupname>", 
                            "Connect this client to a group (if not already connected"}},
                {"disconnect", new string[]{"disconnect",
                            "Disconnect this client if connected, do nothing otherwise"}},
                {"exit", new string[]{"exit",
                            "Exit the CLI, disconnecting if necessary"}},
                {"test", new string[]{"test <message>",
                            "Send a message for the server to write to console."}},
                {"create", new string[]{"create <path> <data>",
                            "Create a new node at the given path with the given data."}},
                {"set", new string[]{"set <path> <data>",
                "Set the data at the node at <path> to <data>, fails if node does not exist"}},
                {"get", new string[]{"get <path>",
                            "Return information (including data) about the node at <path>"}},
                {"ls", new string[]{"ls <path>",
                        "Display a comma-delimited list of the childred of the node at <path>"}},
                {"ls2", new string[]{"ls2 <path>",
                        "Return information (excluding data) about the node at <path>"}},
                {"rmr", new string[]{"rmr <path>",
                        "Delete (recursively) the node at <path>"}},
                {"help", new string[]{"help [cmd]",
                        "Display information about all commands, or the specific one chosen"}}
        };

        static void initCLI(String groupname = "") {
		    if(string.IsNullOrWhiteSpace(groupname)) {
                client = null;
            } else {
                connect(new string[]{groupname});
            }
            isRunning = true;
        }

        static void initCommands() {
			// Setup delegate dictionary
			dwarfFuns = new Dictionary<string, dwarfFun>();
			dwarfFuns["connect"] = (dwarfFun)connect;
			dwarfFuns["disconnect"] = (dwarfFun)disconnect;
			dwarfFuns["exit"] = (dwarfFun)exit;
            dwarfFuns["test"] = (dwarfFun)test;
            dwarfFuns["create"] = (dwarfFun)create;
            dwarfFuns["set"] = (dwarfFun)setNode;
            dwarfFuns["get"] = (dwarfFun)getNode;
            dwarfFuns["ls"] = (dwarfFun)getChildren;
            dwarfFuns["ls2"] = (dwarfFun)getChildren2;
            dwarfFuns["rmr"] = (dwarfFun)delete;
            dwarfFuns["help"] = (dwarfFun)help;
        }

		/** Gets current running status.
		 *
		 * @return Running status
		 */
		static bool getRunningStatus()
		{
			return isRunning;
		}
        
        static string help(string[] args) {
            string helpstring = "";
            if(args.Length > 0) {
                try {
                    string[] info = helpStrings[args[0]];
                    helpstring = string.Format(
                            "{0,-10}{1,5}{2}\n{1,15}{3}",
                            args[0], "", info[0], info[1]);
                    return helpstring;
                } catch (KeyNotFoundException knfe) {
                    helpstring += args[0] + " not valid command\n";
                }
            }
            
            StringBuilder sb = new StringBuilder(helpstring);
            foreach (string k in helpStrings.Keys) {
                string[] info = helpStrings[k];
                sb.Append(string.Format(
                            "{0,-10}{1,5}{2}\n{1,15}{3}\n",
                            k, "", info[0], info[1]));
            }
            return sb.ToString();
        }

        static string create(string[] args) {
            if(args.Length < 2) {
                return "Error: Not enought aruments";
            }
            string retstr = client.create(args[0], args[1]);
            return retstr;
        }

        static string setNode(string[] args) {
            if(args.Length < 2) {
                return "Error: Not enought aruments";
            }
            string retstr = client.setNode(args[0], args[1]);
            return retstr;
        }

        static string getNode(string[] args) {
            DwarfStat stat = client.getNodeAll(args[0]);
            return stat.ToString();
        }

        static string getChildren(string[] args) {
            string retstr = client.getChildren(args[0]);
            return retstr;
        }
        static string getChildren2(string[] args) {
            DwarfStat stat = client.getChildren2(args[0]);
            return stat.ToString();
        }

        static string delete(string[] args) {
            List<string> retlst = client.delete(args[0]);
            return retlst[0];
        }

        static string test(string[] args) {
            string retstr = client.test(args[0]);
            return retstr;
        }

		/** Disconnects (closes connection to) the server.
		 *
		 *
		 */
		static string disconnect(string[] args)
		{
			client.disconnect();
            prompt = "DwarfKeeper>>";
			return "Disconnected";
		}

		/** Connects to the server.
		 *
		 * @param args Arguments regarding connection attempts/timeout
		 */
		static string connect(string[] args)
		{
            if(client != null) {
                return "Client is already connected to a server.";
            }

            client = new DwarfClient(args[0]);
            prompt = string.Format("{0}>>", args);
            return string.Format("Connected to {0}", args);
		}

		/** Disconnects from server and exits the interface.
		 *
		 * @param args Arguments to pass to disconnect().
		 */
		static string exit(string[] args)
		{
			disconnect(args);
			isRunning = false;
			return "Goodbye";
		}
        
        static string handleCMD(string input) {
            string cmd;
            string[] args;

            if(String.IsNullOrWhiteSpace(input)) {
                return "";
            }

            string[] cmdAndArgs = 
                input.Split(new char[]{' '}, 2, StringSplitOptions.RemoveEmptyEntries);

            
            cmd = cmdAndArgs[0];

            if(!isRunning && !cmd.EndsWith("connect")) {
                return "Client is not connected";
            }

            if (1 == cmdAndArgs.Length) {
                if(!noArgCmds.Contains(cmd)) {
                    return "Arguments required for cmd: " + cmd;
                }
                args = new string[]{};
            } else {
                args=cmdAndArgs[1].Split(new char[]{' '},StringSplitOptions.RemoveEmptyEntries);
            }

            try {
                return dwarfFuns[cmd](args);
            } catch (KeyNotFoundException knfe) {
                //TODO: Handle - Print help msg?
            }
            return "";
        }

        static void eventLoop() {
            while(isRunning) {
				Console.Write("\n " + prompt + " "); // Prompt
				string line = Console.ReadLine(); // Get string from user
                Console.WriteLine(handleCMD(line));
            }
        }

		static void Main(string[] args) 
		{
            initCommands();

            if(0 == args.Length) {
                initCLI("");
            } else {
                initCLI(args[0]);
            }

            eventLoop();

		}
	}
}

