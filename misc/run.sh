
# Run several instances of our application
# to create a simple 3-node linear topology A <-> B <-> C.
PORTA=`echo "$NETPORT + 0*5000" | bc`
PORTB=`echo "$NETPORT + 1*5000" | bc`
PORTC=`echo "$NETPORT + 2*5000" | bc`
PROCS=

mono ./peerster.exe -port:$PORTA :$PORTB \
	& PROCS="$PROCS $!"
mono ./peerster.exe -port:$PORTB :$PORTA :$PORTC \
	& PROCS="$PROCS $!"
mono ./peerster.exe -port:$PORTC :$PORTB \
	& PROCS="$PROCS $!"

echo "Started processes $PROCS"

# Kill all the child processes we started and exit.
killprocs() {
	kill $PROCS
	exit $?
}

# Wait for the user to press Enter or CTRL-C.
trap killprocs SIGINT
read -p "Press Enter to terminate Peerster processes."
killprocs

