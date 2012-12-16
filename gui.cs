
using System;

using com.trolltech.qt.core;
using com.trolltech.qt.gui;

// This class implements the main window for the group-chat application.
public class Gui : QDialog
{
	QTextEdit textview;
	QLineEdit textline;
	//QListView table;
	QStringListModel model;
	
	// associated net object
	Net netCpy;

	// The Gui class will fire this event whenever a message is entered.
	public delegate void MessageHandler(Gui sender, String msg);
	public event MessageHandler messageEntered;

	public Gui(Net net) {
		net.newMsg += this.addMessage;
		
		
		// copy the net object so all methods can access it
		netCpy = net;
		
		setWindowTitle("Group-chat");

		// Read-only text box where we display messages from everyone.
		// This widget expands both horizontally and vertically.

		// MULTI line text edit
		textview = new QTextEdit(this);
		textview.setReadOnly(true);

		// Small text-entry box the user can enter messages.
		// This widget normally expands only horizontally,
		// leaving extra vertical space for the textview widget.
		//
		// Challenge!  Change this into a read/write QTextEdit,
		// so that the user can easily enter multi-line messages.

		// single line text edit
		textline = new QLineEdit(this);

		// Create the list of nodes
		//table = new QListView(this);

  		model = new QStringListModel();
		//model.setStringList(net.peerNames);

		// Lay out the widgets to appear in the main window.
		// For Qt widget and layout concepts see:
		// http://doc.trolltech.com/4.6/widgets-and-layouts.html
		
		// Q Vertical Box layout
		QVBoxLayout layout = new QVBoxLayout();
		layout.addWidget(textview);
		layout.addWidget(textline);

		// add objectst to layout tehn

		QVBoxLayout peerLayout = new QVBoxLayout();

		QLabel title = new QLabel("List of peers:");
		peerLayout.addWidget(title);
		
		
		
		for(int i=0; i< net.peerList.size(); i++){
			String peerName = (String)net.peerList.get(i);
			Console.WriteLine(peerName);
			QLabel label = new QLabel(peerName);
			peerLayout.addWidget(label);
		}
		
		QPushButton button = new QPushButton("Add Peer");
		
		QPushButton sendButton = new QPushButton("Send Message");
		sendButton.clicked.connect(this,"gotReturnPressed()");

		button.clicked.connect(this,"addPeer()");
		
		
		QVBoxLayout buttonLay = new QVBoxLayout();
		buttonLay.addWidget(sendButton);
		buttonLay.addWidget(button);
		QWidget buttonW = new QWidget(this);
		buttonW.setLayout(buttonLay);

		
		QWidget peers = new QWidget(this);
		peers.setLayout(peerLayout);
		
		QWidget msgAndInput = new QWidget(this);
		msgAndInput.setLayout(layout); 
		
		

		
		QHBoxLayout window = new QHBoxLayout();
		window.addWidget(peers);
		window.addWidget(msgAndInput);
		window.addWidget(buttonW);
		
		
		base.setLayout(window);
		
		
		

		//layout.addWidget(table);
		
		// base is like "this" in Java, base object of GUI
		//base.setLayout(layout);

		// Register a callback on the textline's returnPressed signal
		// so that we can send the message entered by the user.
		// Note that here we're using a Qt signal, not a C# event.
		// The Qt Jambi bindings for C# don't support custom signals,
		// only the the signals built-in to "native" Qt objects.
		// Thus, any new signals we need to define should be C# events;
		// see the example below.
		
		// removed this functionality and replaced with a button
		
		//textline.returnPressed.connect(this, "gotReturnPressed()");

		// Lab 1: Insert code here to add some kind of GUI facility
		// allowing the user to view the list of peers available,
		// as maintained by the Net instance provided above.
		// You might do this simply by adding a widget to this dialog,
		// or by adding a button or menu that opens a new dialog
		// to display the list of peers, or whatever method you prefer.
		
		
		
	}
	

	private void addPeer(){
		String text = QInputDialog.getText(this, "Add new peer", "PeerHost:Port");
		try{
			if((text.Split(":".ToCharArray()).Length) != 2){
				throw new Exception("Invalid input format for peer");	
			}
			netCpy.addPeerJ(text);
			Console.WriteLine("Added peer");
			//netCpy.printAllPeers();
		} catch (Exception e){
			Console.WriteLine("Unable to add peer");
		}
	}
	

	// Display a message we have received from some node (or ourselves).
	public void addMessage(String source, String msg) {
		textview.append("<b>" + source + ":</b> " + msg + "\n");
	}

	// Handle the returnPressed() signal on our textline entry box.
	private void gotReturnPressed() {
		// Here is an example of how to use a custom C# event.
		// This function gets called by Qt's signal mechanism;
		// in response we pull the text string out of the textline,
		// clear the textline, and forward the string to a C# event.
		//
		// Note that forwarding the event is the LAST thing we do:
		// it is good practice to do this whenever possible,
		// because it avoids the possibility of subtle bugs
		// that might occur if the event handler ends up
		// calling back into this class recursively.
		//
		// Note also that we make a local copy of the MessageHandler
		// BEFORE we actually test it for null and call it.
		// This is good practice in case of multithreaded access,
		// as the event might change between the test and the call.
		String msg = textline.text();	// Get a copy of the string
		textline.clear();		// Clear the textbox
		MessageHandler h = messageEntered;
		if (h != null){			// Forward it as a C# event,
			h(this, msg);		// only if there is a handler.
		}else{
			//Console.WriteLine("Message entered: " + msg);
			addMessage("myself", msg);
			// Pass message from GUI to net class
			netCpy.msgFromGui(msg);
		}
	}
}

