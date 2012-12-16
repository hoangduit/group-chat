
using System;
using System.Collections.Generic;

using com.trolltech.qt.core;
using com.trolltech.qt.gui;

public class AppMain
{
	// Application config info to be parsed out of command-line arguments
	static List<string> initPeers = new List<string>();

	public static void Main(string[] args) {
		QApplication.initialize(args);

		// Parse any command-line arguments we're interested in.
		// For now, we just assume all arguments are hostnames
		// or IP addresses of peers we want to connect to.
		// This might change if/when we need more interesting options.
		ushort port = 0;	// 0 means system-chosen port (default)
		foreach (string arg in args) {
			if (arg.StartsWith("-port:")) {
				port = UInt16.Parse(arg.Substring(6));
			} else if (arg.StartsWith("-")) {
				Console.WriteLine(
					"Unknown command-line option" + arg);
			} else	// Interpret the argument as a peer name.
				initPeers.Add(arg);
		}

		// Create the Net object implementing our network protocol
		// and our peer-to-peer system state model.
		Net net = new Net(port, initPeers);

		// Create and show the main GUI window
		// The Gui object can "see" the Net object but not vice versa.
		// This is intentional: we want to keep Net independent of Gui,
		// so that (for example) we can run a non-graphical instance
		// of our peer-to-peer system controlled some other way,
		// e.g., as a daemon or via command-line or Web-based control.
		Gui gui = new Gui(net);
		gui.show();

		QApplication.exec();
	}
}

