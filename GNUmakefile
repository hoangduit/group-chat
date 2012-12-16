#
# This makefile system follows the structuring conventions
# recommended by Peter Miller in his excellent paper:
#
#	Recursive Make Considered Harmful
#	http://aegis.sourceforge.net/auug97.pdf
#
# Copyright (C) 2003 Massachusetts Institute of Technology 
# See section "MIT License" in the file LICENSES for licensing terms.
# Primary authors: Bryan Ford, Eddie Kohler, Austin Clemens
#

ifdef LAB
SETTINGLAB := true
else
-include conf/lab.mk
endif

-include conf/env.mk

ifndef SOL
SOL := 0
endif
ifndef LABADJUST
LABADJUST := 0
endif


TOP = .

# The C-sharp compile and other commands.
CS	:= gmcs
MONO	:= mono
PERL	:= perl
BASH	:= bash

# C-Sharp compiler flags.
# The -r option references any assemblies we build on (Qt Jambi in this case).
CSFLAGS := -r:qtjambi-4.6.3.dll,IKVM.OpenJDK.Core.dll

# Path to the QtJambi libraries (modify to build/run elsewhere)
export LD_LIBRARY_PATH=/c/cs426/tools/lib
export MONO_PATH=/c/cs426/tools/lib


# C-sharp source files comprising group-chat application.
CSFILES :=	main.cs net.cs gui.cs \
		conf/netconf.cs
CSEXE :=	peerster.exe


# Make sure that 'all' is the first target
all: $(CSEXE)

# Build the main P2P app executable
$(CSEXE): $(CSFILES)
	$(CS) $(CSFLAGS) -out:$@ $(CSFILES)

# Run it under the Mono VM
run: all
	$(BASH) misc/run.sh 
#	$(MONO) ./$(CSEXE) $(PEERS)


# Eliminate default suffix rules
.SUFFIXES:

# Delete target files if there is an error (or make is interrupted)
.DELETE_ON_ERROR:



# Include Makefrags for subdirectories
#include boot/Makefrag



# Configure a default UDP port that depends on the current user ID,
# so that students testing on the same machine hopefully won't conflict.
export NETPORT := $(shell expr `id -u` % 5000 + 40000)
conf/netconf.cs: GNUmakefile
	@test -d conf || mkdir conf
	echo >$@ "internal class NetConf {"
	echo >>$@ "	public static readonly ushort defaultPort = $(NETPORT);"
	echo >>$@ "}" 


# For deleting the build
clean:
	rm -rf $(CSEXE) conf/netconf.cs

distclean: clean
	rm -rf lab$(LAB).tar.gz grade-log

grade: grade-lab$(LAB).sh
	$(V)$(MAKE) clean >/dev/null 2>/dev/null
	$(MAKE) all
	sh grade-lab$(LAB).sh

tarball: realclean
	tar cf - `find . -type f | grep -v '^\.*$$' | grep -v '/\.git/' | grep -v 'lab[0-9].*\.tar\.gz'` | gzip > lab$(LAB)-handin.tar.gz

always:
	@:

.PHONY: all always \
	handin tarball clean clean-labsetup distclean grade labsetup

