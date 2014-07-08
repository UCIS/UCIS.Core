README
======
The UCIS.Core library provides several relatively small reuseable .Net
components.

License
-------
See LICENSE.txt for licensing information.

UCIS.FBGUI
----------
FBGUI consists of components to build a platform-independent graphical user interface. Components
are built in pure C# code and use the System.Graphics functions to draw directly to a framebuffer.

UCIS.NaCl
---------
This namespace contains a fairly straightforward port of some of the cryptographic functions found
in the NaCl library (http://nacl.cace-project.eu/). See also: http://wiki.ucis.nl/NaCl. The
UCIS.NaCl.v2 namespace provides some friendly wrappers around the commonly used low-level functions.

UCIS.Net.HTTP
-------------
This namespace provides simple HTTP webserver components, including a WebSocket server.

UCIS.Remoting
-------------
UCIS.Remoting is a custom .Net remoting solution. It needs only one single Stream channel to
provide remote access to objects. Delegates, serializable types, MarshalByReference types and
interfaces are supported. Garbage Collection is used for lifetime management. The simplicity of the
protocol allows for compatibility with other programming languages, like PHP and JavaScript.

UCIS.USBLib
-----------
USBLib provides various functions to use USB devices in a platform independent way. Direct USB
communication is supported through libusb, WinUSB, VBoxUSB and USBIO drivers on Windows, and via
libusb on Linux systems. The UCIS.USBLib.Descriptor namespace provides classes for decoding USB
descriptor data. The UCIS.HWLib.Windows namespace provides managed access to the Windows Device Tree
and the Windows USB Device Tree.

UCIS.VNCServer
--------------
A simple VNC Server implementation serving a "virtual" VNC desktop. It can be used with FBGUI to
create simple remote graphical user interfaces, or can be connected to some other source of bitmap
data.

Contact
-------
E-mail: Ivo@UCIS.nl
IRC: Ivo in #Chat on irc.kwaaknet.org
WWW: www.ucis.nl