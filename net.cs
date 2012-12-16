
using System;
using System.Text;
using System.Collections.Generic;

using com.trolltech.qt.core;
using com.trolltech.qt.network;
using System.Text.RegularExpressions;


enum PacketType : byte {
	Ping			= 0x01,
}

public class Peer : QObject {

	// Back-reference to Net instance this Peer is associated with.
	public readonly Net net;

	// The peer's (immutable) name host name or IP address,
	// possibly followed by a colon and a numeric port number.
	public string name;
	
	// hostname followed by port
	public string betterName;
	
	// IP addr of peer
	public String IPaddr;
	

	// Private, parsed-out host name and UDP port number.
	// We want these private so we can extend the peer name syntax later.
	private readonly string hostName;
	public readonly ushort port;

	// This object provides asynchronous DNS name to IP address resolution,
	// yielding a list of IP addresses associated with a named host.
	// (Note that a single DNS hostname may have multiple IP addresses.)
	private QHostInfo hostinfo;
	public java.util.List addrs;


	public Peer(Net peerNet, String peerName) {
		net = peerNet;
		name = peerName;
		

		// Does the peerName include a colon and a port number?
		// If so, parse it and strip it off.
		int colonpos = peerName.IndexOf(':');
		if (colonpos >= 0) {		// Explicit port number.
			hostName = peerName.Substring(0, colonpos);
			port = ushort.Parse(peerName.Substring(colonpos+1));
			
		} else {			// Use default port.
			hostName = peerName;
			port = NetConf.defaultPort;
		}

		// If the peer name is empty (aside from port number),
		// interpret it as a synonym for "localhost".
		if (hostName == "")
			hostName = "localhost";

		// Start DNS-resolving this peer's hostname,
		// calling our lookupDone() method when finished.
		Console.WriteLine("Resolving hostname " + hostName);
		QHostInfo.lookupHost(hostName, this, "lookupDone");
		
		
		betterName = hostName + ":" + port;
	}

	// Qt's asynchronous DNS resolution mechanism will call this method
	// when the DNS resolution for this peer's hostname is complete.
	// "Completion" might mean either success or failure:
	// on failure, we'll get a QHostInfo object with no IP addresses.
	public void lookupDone(QHostInfo hi) {
		hostinfo = hi;
		addrs = hi.addresses();
		Console.WriteLine("Peer " + name
			+ ": got " + addrs.size() + " addresses");
		
		//Console.WriteLine(((QHostAddress)addrs.get(0)).ToString());
		IPaddr = ((QHostAddress)addrs.get(0)).ToString();
	}

	// Send a datagram to this peer,
	// using any and all IP addresses we have for it.
	// This isn't very friendly to hosts with multiple IP addresses:
	// we really should figure out which address works and use that one.
	public void send(byte[] msg) {

		// Note that Qt Jambi converts QList into java.util.List,
		// which unfortunately isn't compatible with C#'s foreach,
		// and also isn't a generic that preserves the content type.
		// So we have to iterate manually over the list,
		// and cast each element we get to a QHostAddress explicitly.
		for (int i = 0; i < addrs.size(); i++) {
			net.sock.writeDatagram(msg,
				(QHostAddress)addrs.get(i), port);
		}
	}
}

public class Net : QObject {

	// List of nodes we (currently) consider peers,
	// i.e., with whom we gossip messages and such.
	internal List<Peer> peers = new List<Peer>();

	// UDP socket we'll use to send and receive messages.
	internal QUdpSocket sock = new QUdpSocket();

	// Identifying information for this node, mainly for debugging:
	// myName might not be unique if we don't have a real DNS name.
	internal readonly ushort myPort;
	internal readonly string myName;
	//internal readonly string myIP;
	
	// Buffer we'll receive datagrams into
	private static readonly int recvmax = 9*1024;	// 9KB max datagram size
	private QUdpSocket.HostInfo recvsrc = new QUdpSocket.HostInfo();
	private byte[] recvbuf = new byte[recvmax];

	public java.util.List peerList = new java.util.ArrayList();

	public void addPeerJ(String peerName){
		peers.Add(new Peer(this, peerName));	
	}
	
	public void printAllPeers(){
		Console.WriteLine("Peers are:");
		for(int i=0; i< peers.Count; i++){
			Console.WriteLine(peers[i].betterName);
		}
	}
	
	public Net(ushort initPort, IList<string> initPeers) {
	
		// For each peer name in the command-line arguments,
		// create a Peer object to represent and track that peer.
		foreach(string peerName in initPeers){
			peers.Add(new Peer(this, peerName));
			//peerList.add(peerName);
		}
		
		//Console.WriteLine(peers.Count);
		
		for(int i=0; i< peers.Count; i++){
			peerList.add(peers[i].betterName);
		}
		
		// Bind our UDP socket to the specified (or default) port
		// allowing us to send and receive network datagrams.
		if (initPort == 0)
			initPort = NetConf.defaultPort;
		
		// Like println in Java		
		Console.WriteLine("Binding to port " + initPort);

		sock.bind(new QHostAddress(QHostAddress.SpecialAddress.Any),
			initPort);
		sock.readyRead.connect(this, "receive()");

		// Save some identifying information for printfs and such.
		myPort = initPort;
		myName = QHostInfo.localHostName() + ":" + myPort;
		
		// Create a timer to trigger repeating "pings"
		// we send out to to all our peers.
		QTimer timer = new QTimer(this);
		timer.timeout.connect(this, "pingAllPeers()");
		timer.start(1000);	// ping once every second
	}

	// contains the message data to be passed up to the GUI
	public MsgStorage msg;
	
	// The Gui class will fire this event whenever a message is entered.
	public delegate void NewMsgHandler(String source, String msg);
	public event NewMsgHandler newMsg;	
	
	// List of already forwarded message IDs
	private List<String> seenIDs = new List<String>();
			
	// This is used to store which messages we've got Acks for
	// add message ID when send a message, add srcs when receive Acks
	//private List<List<String>> msgIdToSrc = new List<List<String>>();
	
	// We connect our UDP socket's readyRead() signal to this method,
	// so this method gets called whenever packets have been received
	// that are waiting to be read.
	private void receive() {
		while (true) {
			int len = sock.readDatagram(recvbuf, recvsrc);
			if (len < 0)
				break;	// no more datagrams to receive

			// Extract the datagram's source address and port
			QHostAddress srcaddr = recvsrc.address;
			int srcport = recvsrc.port;
			
			
			//Console.WriteLine(myName + ": datagram of size " + len +
			//	" received from " + srcaddr + ":" + srcport);
			
			byte[] recvbufLen = new byte[len];
			for(int i=0; i<len;i++){
				recvbufLen[i] = recvbuf[i];	
			}
			
			// Check not a ping
			ASCIIEncoding enc = new ASCIIEncoding();
			String msg = enc.GetString(recvbufLen);
			
			if(!msg.Equals("PING")){
				try{
					// now check whether it's and ACK
					if(msg.Contains("Control: ihave")){
						// ACK packet
						msg = msg.Replace("\r\n", "");
						String[] lines = msg.Split(" ".ToCharArray());
						// ignore first two elements of lines as these are control: and ihave
						for(int i=2; i<lines.Length; i++){
							if(lines[i].Length>0){
								MsgStorage doneMsg = getMsgStorage(lines[i],findPeer(srcaddr.ToString(), srcport));
								// set all messages acked to be done
								doneMsg.setDone();
							}
						}

					}else{
					
						MsgStorage msgObj = new MsgStorage(recvbufLen);	
						
						// Send an Ack
						String ackStr = "Control: ihave ";
						ackStr = ackStr + msgObj.getMsgId();
												
						MsgStorage ack = new MsgStorage(ackStr, findPeer(srcaddr.ToString(), srcport));
						//Console.WriteLine(ack.ackInfo);
						
						//Console.WriteLine(msg);

													
						// remove first part of boolean test below to test retransmit
						if(seenIDs.Contains(msgObj.getMsgId()) || msgObj.getFromWho().Equals(myName)){
							// already seen the message so ignore it	
							// or I sent it so ignore
						}else{
							// Only fire if new msg id
							NewMsgHandler newHandler = newMsg;
							if (newHandler != null){			// Forward it as a C# event,
								newHandler(msgObj.getFromWho(), msgObj.getMsgBody());		// only if there is a handler.
							}
							sendToPeers(msgObj);
							
							
						}	
					}
				}catch(Exception e){
					Console.WriteLine("Received bad packet, dropping it");
					Console.WriteLine(msg);
					//Console.WriteLine(e.ToString());
				}
			}	
		}
	}
	
	public Peer findPeer(String src, int port){
		//Console.WriteLine("Looking for peer");
		foreach(Peer p in peers){	
			// Want s2 to be IP NOT betterName
			//String s2 = p.betterName;
			String s2 = p.IPaddr;
			s2 += ":";
			s2 += p.port;
			//s2 = s2.Replace("localhost","127.0.0.1");
			//Console.Write(s2);
			//Console.WriteLine(src + ":" + port);
			if(s2.Equals(src + ":" + port)){
				// have a match
				return p;
			}			
		}		
		throw new Exception("Couldn't find Peer object to send Ack to");
	}
	
	List<MsgStorage> sentMsgs = new List<MsgStorage>();
	
	// this takes a message id and peer and returns the corresponding msgstorage object
	private MsgStorage getMsgStorage(String msgId, Peer p){
		
		
		//if(src.Equals("127.0.0.1")){
		//	src = "localhost";
		//}
		
		foreach(MsgStorage m in sentMsgs){
			//if(m.getToPeer().betterName.EqualsipAddr.Equals(src + ":" + port)){
				//Console.Write(m.getToPeer().betterName);
				//Console.WriteLine(p.betterName);
			if(m.getToPeer().betterName.Equals(p.betterName)){
				// check msgid
				if(msgId.Equals(m.getMsgId())){
					//Console.WriteLine("YEAH");	
					return m;
				}
			}
				/*if(m.getMsgId().Equals(msgId)){
					// have a match
					return m;
				}*/
			//}
		}
			
		throw new Exception("Couldn't find MsgStorage object for Ack");
	}
	
	private void sendToPeers(MsgStorage msg){
		//Console.WriteLine(msg.getMsgId());
		if(!seenIDs.Contains(msg.getMsgId())){
			foreach(Peer peer in peers){
				// Set up a new msg storage object with the current peer
				MsgStorage msg2 = new MsgStorage(msg.getEncodedMessage());
				// set the peer we send message to
				msg2.setToPeer(peer);
				msg2.ipAddr = "sdfjksdfsdf";//(String)(peer.addrs.get(0).toString());
				// add message to sent list
				sentMsgs.Add(msg2);
				// start the timeout
				msg2.startTimer();
				peer.send(msg2.getEncodedMessage());	
				//Console.WriteLine(peer.betterName);
			}
		}
		seenIDs.Add(msg.getMsgId());
	}
	

	private void pingAllPeers() {
		ASCIIEncoding enc = new ASCIIEncoding();
		byte[] msg = enc.GetBytes("PING");

		foreach(Peer peer in peers)
			peer.send(msg);
	}
	
	public void msgFromGui(String msg){
			
		// this is WRONG!
		MsgStorage m = new MsgStorage(msg, myName, "sdfsdfsd");

		sendToPeers(m);

		MsgStorage m2 = new MsgStorage(m.getEncodedMessage());
		
	}
	
}

public class MsgStorage : QObject{
	public String ipAddr;
	private String msgId;
	private String msgBody;
	private String fromWho;
	// don't know what this is for??
	private Peer toPeer;
	private int timeout = 1000; // don't forget millis not seconds
	private int maxTimeout = 30000;
	private bool didAckJ = false;
	private bool doneJ = false;
	private QTimer timer;
	public String ackInfo;
	//private Net net;
	
	/*// sends the current message to all the peers
	public void sendToPeers(){
		foreach(Peer peer in net.peers)
			peer.send(getEncodedMessage());			
	}*/
	
	public void sendToPeer(){
		this.toPeer.send(this.getEncodedMessage());
	}
		
	// getters
	public String getMsgId(){
		return msgId;
	}
	public String getMsgBody(){
		return msgBody;
	}
	public String getFromWho(){
		return fromWho;
	}
	public Peer getToPeer(){
		return toPeer;
	}	
	public int getTimeout(){
		return timeout;
	}	
	public int getMaxTimeout(){
		return maxTimeout;
	}	
	public bool isAcked(){
		return didAckJ;
	}	
	public bool isDone(){
		return doneJ;
	}	
	
	// setters
	public void setMsgId(String s){
		msgId = s;
	}
	public void setMsgBody(String s){
		msgBody = s;
	}
	public void setFromWho(String s){
		fromWho = s;
	}
	public void setToPeer(Peer p){
		toPeer = p;
	}	
	public void setTimeout(int x){
		timeout = x;
	}	
	public void setMaxTimeout(int x){
		maxTimeout = x;
	}	
	public void setAcked(){
		didAckJ = true;
	}	
	public void setDone(){
		doneJ = true;
	}	
	
	public void incrementTimeout(){
		this.timeout = this.timeout * 2;
	}
	
	public void startTimer(){
		timer = new QTimer(this);
		// only fire the timer once
		timer.setSingleShot(true);
		timer.timeout.connect(this, "reSend()");
		timer.start(timeout);	// go after 1 second
	}
	
	private void reSend(){
		if(!doneJ){
			Console.WriteLine("Resending");
			if(timeout<= this.maxTimeout){
				this.toPeer.send(this.getEncodedMessage());
				incrementTimeout();
				startTimer();
			}else{
				// give up
				this.doneJ = true;
			}
		}else{
			
		}
	}
	
	// Constructor takes string and sets up internal representation
	public MsgStorage(String s, String myName, String ip){
		this.msgId = "<" + DateTime.Now.Ticks + "@" + myName.Replace(":","-") + ">";
		this.msgBody = s;
		this.fromWho = myName;
		this.ipAddr = ip;
		//this.net = net;
	}
	
	// Constructor to make an Ack and send it
	public MsgStorage(String s, Peer p){
		this.ackInfo = s;
		this.toPeer = p;
		ASCIIEncoding enc = new ASCIIEncoding();
		byte[] arr = enc.GetBytes(s);
		p.send(arr);
	}
	
	
	
	
/*	// Constructor takes byte array unwraps it
	public MsgStorage(String encoded, bool b){		
		String header = getHeaderFromEncoded(encoded);
		
		//Console.WriteLine(header);
		
		this.fromWho = getFromFromHeader(header);
		this.msgId = getIDFromHeader(header);
		this.msgBody = getBodyFromEncoded(encoded);
	}
*/	
	
	
	
	
	// Constructor takes byte array unwraps it
	public MsgStorage(byte[] arr){
		String encoded = bytesToEnc(arr);
		String header = getHeaderFromEncoded(encoded);
		
		//Console.WriteLine(header);
		
		this.fromWho = getFromFromHeader(header);
		this.msgId = getIDFromHeader(header);
		this.msgBody = getBodyFromEncoded(encoded);
	}
	
	
	private String getBodyFromEncoded(String input){
		//String[] lines = encoded.Split("\r\n\r\n\r\n".ToCharArray());
		
	string pattern = "(\r\n\r\n)";

	string[] substrings = Regex.Split(input, pattern);    // Split 
	
		return substrings[2];
	}
	
	
	
	private String getHeaderFromEncoded(String input){
	string pattern = "(\r\n\r\n)";

	string[] substrings = Regex.Split(input, pattern);    

		
		return substrings[0];
	}
	
	// takes the decoded header and returns the From field
	private String getFromFromHeader(String header){
		String[] lines = header.Split("\r\n".ToCharArray());
		for(int i=0; i<lines.Length; i++){
			if(lines[i].Contains("From:")){
				String[] line = lines[i].Split(":".ToCharArray());
				// append remainder of array into a string
				String fromField = "";
				for(int j=1; j<line.Length; j++){
						fromField = fromField + ":" + line[j];
				}
				if(fromField.StartsWith(": ")){
					fromField = fromField.Substring(2);
				}
				return fromField;
			}
		}
		// if we get here there is no from field found
		return "";
	}
	
	// takes the decoded header and returns the Message-ID field
	private String getIDFromHeader(String header){
		
		
		String[] lines = header.Split("\r\n".ToCharArray());
		for(int i=0; i<lines.Length; i++){
			
			if(lines[i].Contains("Message-ID:")){
				//Console.WriteLine(lines[i]);
				String[] line = lines[i].Split(":".ToCharArray());
				// append remainder of array into a string
				String fromField = "";
				for(int j=1; j<line.Length; j++){
						fromField = fromField + ":" + line[j];
				}
				if(fromField.StartsWith(": ")){
					fromField = fromField.Substring(2);
				}
				return fromField;
			}
		}
		// if we get here there is no from field found
		return "";
	}
	

	

	
	private String bytesToEnc(byte[] arr){
		ASCIIEncoding enc = new ASCIIEncoding();
		return enc.GetString(arr);	
	}
	
	public byte[] getEncodedMessage(){
		return encode(getMsgNHeader());
	}
	
	// this returns the message header
	private String getMsgNHeader(){
		return  getHeader() + "\r\n" + "\r\n" + this.msgBody;
	}
	
	// this returns the message header
	private String getHeader(){
		if(this.msgId.StartsWith("<")){
			return  "From: " + this.fromWho + "\r\n" + "Message-ID: " + this.msgId;
		}else{
			return  "From: " + this.fromWho + "\r\n" + "Message-ID: <" + this.msgId + ">";
		}
	}
	

	
	// encodes a string into a byte array
	private byte[] encode(String s){
		// now encode ready to send over network
		ASCIIEncoding enc = new ASCIIEncoding();
		byte[] arr = enc.GetBytes(s);
		return arr;
	}
/*	
	// sends a byte array to peers
	private void sendByteToPeers(byte[] arr){
		// now send it out to all the peers!
		foreach(Peer peer in net.peers)
			peer.send(arr);
	}
	
	// takes a message and adds a header
	private String addHeader(String body){
		String encoded =  "From: " + myName + "\r\n" + "Message-ID: <" +  + ">";
		encoded += "\r\n\r\n" + body;
		Console.WriteLine(encoded);	

		return encoded;
	}
	*/
}











































