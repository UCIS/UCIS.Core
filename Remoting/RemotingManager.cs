using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Serialization;
using System.Threading;
using UCIS.Util;
using SysThreadPool = System.Threading.ThreadPool;

//[assembly: InternalsVisibleToAttribute("UCIS.Remoting.Proxies")]

namespace UCIS.Remoting {
	public class RemotingManager {
		Dictionary<UInt32, PendingRemoteCall> pendingCalls = new Dictionary<uint, PendingRemoteCall>();
		Dictionary<Thread, UInt32> waitingCallThreads = new Dictionary<Thread, UInt32>();
		public Boolean Closed { get; private set; }

		IDictionary<String, Object> incomingCallContext = new Dictionary<String, Object>();
		[ThreadStatic]
		static IDictionary<String, Object> currentCallContext;

		public event Action<String> OnDebugLog;
		public event Action<Exception> OnErrorLog;
		public event Action<RemotingManager> OnClosed;

		private void DebugLog(String text, params Object[] args) {
			if (OnDebugLog != null) OnDebugLog(String.Format(text, args));
		}
		private void ErrorLog(Exception ex) {
			if (OnErrorLog != null) OnErrorLog(ex);
		}

		public Object LocalRoot { get; set; }

		public IDictionary<String, Object> CallContext { get { return incomingCallContext; } }
		public static IDictionary<String, Object> CurrentCallContext { get { return currentCallContext; } }

		public RemotingManager(PacketStream stream) : this(stream, null) { }
		public RemotingManager(PacketStream stream, Object localRoot) {
			this.stream = stream;
			this.LocalRoot = localRoot;
			this.Closed = false;
			stream.BeginReadPacketFast(ReceiveCallback, null);
		}

		#region I/O, multiplexing, encoding
		PacketStream stream;
		public PacketStream Stream { get { return stream; } }
		int streamIndex = 0;
		Dictionary<int, StreamChannel> streamChannels = new Dictionary<int, StreamChannel>();

		class StreamChannel : QueuedPacketStream {
			public RemotingManager Manager { get; private set; }
			public int StreamID { get; private set; }
			public StreamChannel OtherSide { get; set; }

			public StreamChannel(RemotingManager conn, int sid) {
				this.Manager = conn;
				this.StreamID = sid;
			}

			internal void AddBuffer(Byte[] buffer, int offset, int count) {
				if (Closed) return;
				base.AddReadBufferCopy(buffer, offset, count);
			}

			public override bool CanRead { get { return !Closed; } }
			public override bool CanWrite { get { return !Closed; } }

			public override void Flush() { }

			public override void Write(byte[] buffer, int offset, int count) {
				if (Closed) throw new ObjectDisposedException("BaseStream", "The connection has been closed");
				Manager.WriteStreamChannelPacket(StreamID ^ 0x4000, buffer, offset, count);
			}

			public override void Close() {
				StreamChannel other = OtherSide;
				MultiplexorClosed();
				if (other != null) other.CloseInternal();
				lock (Manager.streamChannels) Manager.streamChannels.Remove(StreamID);
			}
			public void CloseInternal() {
				MultiplexorClosed();
				lock (Manager.streamChannels) Manager.streamChannels.Remove(StreamID);
			}
			internal void MultiplexorClosed() {
				base.Close();
				OtherSide = null;
			}
		}

		private void ReceiveCallback(IAsyncResult ar) {
			if (ar.CompletedSynchronously) {
				SysThreadPool.QueueUserWorkItem(ReceiveCallbackA, ar);
			} else {
				ReceiveCallbackB(ar);
			}
		}
		private void ReceiveCallbackA(Object state) {
			ReceiveCallbackB((IAsyncResult)state);
		}
		private void ReceiveCallbackB(IAsyncResult ar) {
			Boolean isclosed = false;
			try {
				ArraySegment<Byte> packet;
				try {
					packet = stream.EndReadPacketFast(ar);
				} catch (ObjectDisposedException) {
					isclosed = true;
					throw;
				} catch (EndOfStreamException) {
					isclosed = true;
					throw;
				}
				Byte[] array = packet.Array;
				if (packet.Count < 2) throw new ArgumentOutOfRangeException("packet.Count", "Packet is too small");
				int offset = packet.Offset;
				int sid = (array[offset + 0] << 8) | (array[offset + 1] << 0);
				if ((sid & 0x8000) != 0) {
					StreamChannel substr;
					if (streamChannels.TryGetValue(sid, out substr)) substr.AddBuffer(array, offset + 2, packet.Count - 2);
				}
				stream.BeginReadPacketFast(ReceiveCallback, null);
				if (sid == 0) {
					Object obj;
					using (MemoryStream ms = new MemoryStream(packet.Array, packet.Offset + 2, packet.Count - 2, false)) obj = Deserialize(ms);
					ReceiveObject(obj);
				}
			} catch (Exception ex) {
				Closed = true;
				stream.Close();
				lock (ObjectReferencesByID) {
					ObjectReferencesByID.Clear();
					RemoteObjectReferences.Clear();
				}
				lock (pendingCalls) {
					foreach (PendingRemoteCall call in pendingCalls.Values) call.SetError(new InvalidOperationException("The connection has been closed"));
					pendingCalls.Clear();
				}
				lock (streamChannels) {
					foreach (StreamChannel s in streamChannels.Values) s.MultiplexorClosed();
					streamChannels.Clear();
				}
				if (!isclosed) ErrorLog(ex);
				if (OnClosed != null) OnClosed(this);
			}
		}
		private void SendObject(Object obj) {
			if (Closed) throw new ObjectDisposedException("RemotingManager", "The connection has been closed");
			using (MemoryStream ms = new MemoryStream()) {
				ms.WriteByte(0);
				ms.WriteByte(0);
				Serialize(ms, obj);
				lock (stream) ms.WriteTo(stream);
			}
		}

		private void WriteStreamChannelPacket(int sid, Byte[] buffer, int offset, int count) {
			Byte[] store = new Byte[count + 2];
			store[0] = (Byte)(sid >> 8);
			store[1] = (Byte)(sid >> 0);
			Buffer.BlockCopy(buffer, offset, store, 2, count);
			lock (stream) stream.Write(store, 0, store.Length);
		}

		public PacketStream GetStreamPair(out PacketStream remote) {
			StreamChannel stream;
			int sid;
			lock (streamChannels) {
				if (Closed) throw new ObjectDisposedException("BaseStream", "Reading from the base stream failed");
				while (true) {
					sid = Interlocked.Increment(ref streamIndex);
					if ((sid & 0xc000) != 0) streamIndex = sid = 0;
					sid |= 0x8000;
					if (!streamChannels.ContainsKey(sid)) break;
				}
				stream = new StreamChannel(this, sid);
				streamChannels.Add(sid, stream);
			}
			stream.OtherSide = (StreamChannel)SyncCall(new CreateStreamRequest() { StreamObject = stream, StreamID = sid | 0x4000 });
			remote = stream.OtherSide;
			return stream;
		}
		#endregion

		#region Incoming call processing
		private void ReceiveObject(Object obj) {
			if (obj is ReferenceReleaseRequest) {
				ReferenceReleaseRequest req = (ReferenceReleaseRequest)obj;
				lock (ObjectReferencesByID) {
					RemoteObjectReference objref = (RemoteObjectReference)ObjectReferencesByID[req.ObjectID];
					if (objref.Release(req.ReferenceCount) == 0) {
						ObjectReferencesByID.Remove(objref.ID);
						RemoteObjectReferences.Remove(objref);
						DebugLog("Release remoted object {0} with reference count {1}; {2} objects referenced", objref.ID, req.ReferenceCount, RemoteObjectReferences.Count);
					}
				}
			} else if (obj is SynchronousCall) {
				SynchronousCall sc = (SynchronousCall)obj;
				if (sc.IsResult) {
					PendingRemoteCall call;
					lock (pendingCalls) {
						call = pendingCalls[sc.CallID];
						pendingCalls.Remove(call.ID);
					}
					call.SetResult(sc.Data);
				} else if (sc.HasPreviousCallID) {
					PendingRemoteCall call;
					lock (pendingCalls) call = pendingCalls[sc.PreviousCallID];
					if (!call.SetNextCall(sc)) {
						ProcessRemoteCallRequest(sc);
					}
				} else {
					ProcessRemoteCallRequest(sc);
				}
			} else {
				throw new InvalidDataException("Unexpected object type");
			}
		}

		private void ProcessRemoteCallRequest(SynchronousCall call) {
			UInt32 prevcallid;
			Boolean hasprevcallid;
			Thread currentThread = Thread.CurrentThread;
			lock (waitingCallThreads) {
				hasprevcallid = waitingCallThreads.TryGetValue(currentThread, out prevcallid);
				waitingCallThreads[currentThread] = call.CallID;
			}
			IDictionary<String, Object> prevCallContext = currentCallContext;
			currentCallContext = incomingCallContext;
			try {
				call.Data = ProcessRemoteCallRequestA(call.Data);
			} finally {
				currentCallContext = prevCallContext;
				lock (waitingCallThreads) {
					if (hasprevcallid) waitingCallThreads[currentThread] = prevcallid;
					else waitingCallThreads.Remove(currentThread);
				}
			}
			call.IsResult = true;
			SendObject(call);
		}

		private Object FixReturnType(Object value, Type expectedType) {
			if (value == null) return value;
			Type valueType = value.GetType();
			if (valueType != expectedType) {
				if (valueType.IsArray) {
					Array retarray = value as Array;
					if (retarray != null) {
						if (!valueType.GetElementType().IsPublic && retarray.Rank == 1 &&
								(
									expectedType.IsArray ||
									(expectedType.IsGenericType && (expectedType.GetGenericTypeDefinition() == typeof(IEnumerable<>) || expectedType.GetGenericTypeDefinition() == typeof(ICollection<>) || expectedType.GetGenericTypeDefinition() == typeof(IList<>)))
								)
							) {
							Type btype = expectedType.IsArray ? expectedType.GetElementType() : expectedType.GetGenericArguments()[0];
							Array r = Array.CreateInstance(btype, retarray.Length);
							retarray.CopyTo(r, 0);
							value = r;
						}
					}
				}
			}
			return value;
		}
		private static Object FixObjectType(Object obj, Type type) {
			if (ReferenceEquals(obj, null)) return type.IsPrimitive ? Activator.CreateInstance(type) : obj;
			if (RemotingServices.IsTransparentProxy(obj)) return obj;
			Type objtype = obj.GetType();
			if (type == objtype || type.IsAssignableFrom(objtype)) return obj;
			if (objtype.IsArray && ((Array)obj).Rank == 1 && ((Array)obj).GetLowerBound(0) == 0 && (type.IsArray ||
				(type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IEnumerable<>) || type.GetGenericTypeDefinition() == typeof(ICollection<>) || type.GetGenericTypeDefinition() == typeof(IList<>)))
				)) {
				type = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
				Array src = (Array)obj;
				Array dst = Array.CreateInstance(type, src.Length);
				for (int i = 0; i < src.Length; i++) dst.SetValue(FixObjectType(src.GetValue(i), type), i);
				return dst;
			}
			if (obj is IEnumerable<KeyValuePair<String, Object>>) {
				Object inst = null;
				StreamingContext sc = new StreamingContext(StreamingContextStates.All);
				if (typeof(ISerializable).IsAssignableFrom(type) && type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(SerializationInfo), typeof(StreamingContext) }, null) != null) {
					SerializationInfo si = new SerializationInfo(type, new TypeFixerConverter());
					foreach (KeyValuePair<String, Object> kvp in (IEnumerable<KeyValuePair<String, Object>>)obj) si.AddValue(kvp.Key, kvp.Value);
					inst = Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Object[] { si, sc }, null);
				} else if (type.IsSerializable) {
					inst = FormatterServices.GetUninitializedObject(type);
					List<MemberInfo> members = new List<MemberInfo>();
					List<Object> values = new List<Object>();
					foreach (KeyValuePair<String, Object> kvp in (IEnumerable<KeyValuePair<String, Object>>)obj) {
						MemberInfo[] mms = type.GetMember(kvp.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (mms.Length != 1) throw new InvalidOperationException();
						members.Add(mms[0]);
						if (mms[0] is PropertyInfo) {
							values.Add(FixObjectType(kvp.Value, ((PropertyInfo)mms[0]).PropertyType));
						} else if (mms[0] is FieldInfo) {
							values.Add(FixObjectType(kvp.Value, ((FieldInfo)mms[0]).FieldType));
						} else {
							throw new InvalidDataException();
						}
					}
					FormatterServices.PopulateObjectMembers(inst, members.ToArray(), values.ToArray());
				}
				if (inst != null) {
					IObjectReference objref = inst as IObjectReference;
					if (objref != null) inst = objref.GetRealObject(sc);
					IDeserializationCallback deserializationcallback = inst as IDeserializationCallback;
					if (deserializationcallback != null) deserializationcallback.OnDeserialization(null);
					return inst;
				}
			}
			return Convert.ChangeType(obj, type);
		}
		class TypeFixerConverter : FormatterConverter, IFormatterConverter {
			public new Object Convert(Object value, Type type) {
				return FixObjectType(value, type);
			}
		}
		private Object ProcessRemoteMethodCallRequestA(Object ret) {
			if (ret is DelegateCallRequest) {
				DelegateCallRequest call = (DelegateCallRequest)ret;
				Object target = call.Delegate;
				DebugLog("Remote delegate call on {0}", target);
				if (ReferenceEquals(target, null)) throw new NullReferenceException("target");
				if (!(target is Delegate)) throw new InvalidCastException("target");
				Object[] args = call.Arguments;
				return ((Delegate)target).DynamicInvoke(args);
			} else if (ret is MethodCallRequest) {
				MethodCallRequest call = (MethodCallRequest)ret;
				Object target = call.Object;
				if (ReferenceEquals(target, null)) throw new NullReferenceException("target");
				Type intf = call.Type ?? target.GetType();
				DebugLog("Remote call {0}.{1} on {2}", intf.FullName, call.MethodName, target);
				if (!intf.IsInstanceOfType(target)) throw new InvalidCastException("target");
				MethodInfo meth;
				if (call.MethodSignature != null) {
					meth = intf.GetMethod(call.MethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, call.MethodSignature, null);
				} else {
					meth = intf.GetMethod(call.MethodName, BindingFlags.Instance | BindingFlags.Public);
				}
				if (meth == null) throw new NullReferenceException("method");
				Object[] args = call.Arguments;
				Object retval;
				if (meth.Name == "GetType" && meth == typeof(Object).GetMethod("GetType", Type.EmptyTypes)) {
					if (target.GetType().IsPublic) retval = target.GetType();
					else retval = intf;
				} else {
					ParameterInfo[] param = meth.GetParameters();
					for (int i = 0; i < args.Length && i < param.Length; i++) args[i] = FixObjectType(args[i], param[i].ParameterType);
					retval = meth.Invoke(target, args);
				}
				//Todo: causes lots of redundant data transfers, do something about it!
				/*Object[] rargs = new Object[0];
				for (int i = 0; i < args.Length; i++) {
					if (args[i] == null) continue;
					if (args[i].GetType().IsArray) {
						Array.Resize(ref rargs, i + 1);
						rargs[i] = args[i];
					}
				}
				resp.ReturnArguments = rargs;*/
				return FixReturnType(retval, meth.ReturnType);
			} else if (ret is PropertyAccessRequest) {
				PropertyAccessRequest call = (PropertyAccessRequest)ret;
				Object target = call.Object;
				if (ReferenceEquals(target, null)) throw new NullReferenceException("target");
				Type intf = call.Type ?? target.GetType();
				DebugLog("Remote property access {0}.{1} on {2}", intf.FullName, call.PropertyName, target);
				if (!intf.IsInstanceOfType(target)) throw new InvalidCastException("target");
				PropertyInfo meth = intf.GetProperty(call.PropertyName, BindingFlags.Instance | BindingFlags.Public);
				if (meth == null) throw new NullReferenceException("property");
				if (call.SetValue) {
					meth.SetValue(target, call.Value, null);
					return null;
				} else {
					return FixReturnType(meth.GetValue(target, null), meth.PropertyType);
				}
			} else if (ret is EchoRequest) {
				Object obj = ((EchoRequest)ret).Object;
				DebugLog("Remote echo request for {0}", obj);
				return obj;
			} else {
				throw new InvalidDataException("Unexpected object type");
			}
		}
		private Object ProcessRemoteCallRequestA(Object ret) {
			if (ret is DelegateCallRequest || ret is MethodCallRequest || ret is PropertyAccessRequest || ret is EchoRequest) {
				MethodCallResponse resp = new MethodCallResponse();
				try {
					resp.ReturnValue = ProcessRemoteMethodCallRequestA(ret);
				} catch (Exception ex) {
					resp.Exception = ex;
					ErrorLog(ex);
				}
				return resp;
			} else if (ret is GetRootRequest) {
				DebugLog("Remote root request");
				return LocalRoot;
			} else if (ret is ObjectCanCastToRequest) {
				ObjectCanCastToRequest ctr = (ObjectCanCastToRequest)ret;
				Type intf = ctr.Type;
				Object target = ctr.Object;
				DebugLog("Remote type check for {0} on {1}", intf.Name, target);
				return intf != null && intf.IsInstanceOfType(target);
			} else if (ret is CreateStreamRequest) {
				CreateStreamRequest csr = (CreateStreamRequest)ret;
				StreamChannel ss = new StreamChannel(this, csr.StreamID);
				ss.OtherSide = csr.StreamObject;
				lock (streamChannels) streamChannels.Add(csr.StreamID, ss);
				return ss;
			} else {
				throw new InvalidDataException("Unexpected object type");
			}
		}
		#endregion

		#region Outgoing calls
		UInt32 callID = 0;
		private UInt32 AllocatePendingCallID(PendingRemoteCall call) {
			UInt32 cid;
			lock (pendingCalls) {
				while (true) {
					cid = ++callID;
					if (!pendingCalls.ContainsKey(cid)) break;
					Monitor.Wait(pendingCalls);
				}
				call.ID = cid;
				pendingCalls.Add(cid, call);
			}
			return cid;
		}
		private Object SyncCall(Object req) {
			UInt32 previousCall;
			Boolean hasPreviousCall;
			lock (waitingCallThreads) hasPreviousCall = waitingCallThreads.TryGetValue(Thread.CurrentThread, out previousCall);
			PendingRemoteCall pending = new PendingRemoteCall() { Completed = false, WaitHandle = new ManualResetEvent(false), CallData = req };
			AllocatePendingCallID(pending);
			SynchronousCall call = new SynchronousCall() { IsResult = false, CallID = pending.ID, Data = req, HasPreviousCallID = false };
			if (hasPreviousCall) {
				call.HasPreviousCallID = true;
				call.PreviousCallID = previousCall;
			}
			SendObject(call);
			while (true) {
				pending.WaitHandle.WaitOne();
				if (pending.Completed) break;
				pending.WaitHandle.Reset();
				if (pending.NextCall == null) throw new InvalidOperationException("Operation did not complete");
				SynchronousCall sc = pending.NextCall.Value;
				pending.NextCall = null;
				ProcessRemoteCallRequest(sc);
			}
			pending.WaitHandle.Close();
			if (pending.Error != null) throw pending.Error;
			return pending.Response;
		}
		private void AsyncCall(Object req) {
			UInt32 cid = AllocatePendingCallID(new PendingRemoteCall() { Completed = false, CallData = req });
			SendObject(new SynchronousCall() { IsResult = false, CallID = cid, Data = req, HasPreviousCallID = false });
		}

		public Object RemoteRoot {
			get {
				return SyncCall(new GetRootRequest());
			}
		}

		public static void AsyncCall(Delegate f, params Object[] args) {
			IProxyBase proxy;
			if ((proxy = GetRealProxyForObject(f)) != null) {
				proxy.Manager.AsyncCall(new DelegateCallRequest() { Delegate = proxy, Arguments = args });
			} else if ((proxy = GetRealProxyForObject(f.Target)) != null) {
				MethodCallRequest call = new MethodCallRequest() { Object = proxy, Type = f.Method.DeclaringType, MethodName = f.Method.Name, Arguments = args };
				call.MethodSignature = ConvertMethodParameterTypeArray(f.Method);
				proxy.Manager.AsyncCall(call);
			} else {
				throw new InvalidOperationException("Delegate is not a proxy for a remote object");
			}
		}

		private static Type[] ConvertMethodParameterTypeArray(MethodInfo method) {
			ParameterInfo[] parameters = method.GetParameters();
			Type[] types = new Type[parameters.Length];
			for (int i = 0; i < types.Length; i++) types[i] = parameters[i].ParameterType;
			return types;
		}
		private void ProxyCallCheckReturnType(Object value, Type type) {
			if (type == typeof(void)) return;
			if (type.IsInstanceOfType(value)) return;
			if (value == null && !type.IsValueType) return;
			throw new InvalidCastException("Type returned by remote procedure does not match expected type.");
		}
		private void ProxyCallFixReturnArguments(Object[] args, Object[] rargs) {
			if (rargs == null) return;
			for (int i = 0; i < rargs.Length; i++) {
				if (rargs[i] == null) continue;
				if (args[i] != null && args[i].GetType().IsArray) {
					((Array)rargs[i]).CopyTo((Array)args[i], 0);
				}
			}
		}
		private Object ProxyMakeCallDelegate(DelegateProxy obj, Object[] args) {
			DelegateCallRequest call = new DelegateCallRequest() { Delegate = obj, Arguments = args };
			MethodCallResponse resp = (MethodCallResponse)SyncCall(call);
			if (resp.Exception != null) throw new Exception("Remote exception", resp.Exception);
			ProxyCallFixReturnArguments(args, resp.ReturnArguments);
			ProxyCallCheckReturnType(resp.ReturnValue, obj.MethodSignature.ReturnType);
			return resp.ReturnValue;
		}
		private Object ProxyMakeCall(IProxyBase obj, MethodInfo method, Object[] args) {
			/*if (args.Length == 1 && method.ReturnType == typeof(void) && args[0] != null && args[0] is Delegate) {
				foreach (EventInfo ei in type.GetEvents()) {
					if (ei.EventHandlerType.IsInstanceOfType(args[0])) {
						if (method == ei.GetAddMethod()) {
							Console.WriteLine("ADD EVENT");
						} else if (method == ei.GetRemoveMethod()) {
							Console.WriteLine("REMOVE EVENT");
						}
					}
				}
			}*/
			MethodCallRequest call = new MethodCallRequest() { Object = obj, Type = method.DeclaringType, MethodName = method.Name, Arguments = args };
			call.MethodSignature = ConvertMethodParameterTypeArray(method);
			MethodCallResponse resp = (MethodCallResponse)SyncCall(call);
			if (resp.Exception != null) throw new Exception("Remote exception", resp.Exception);
			ProxyCallFixReturnArguments(args, resp.ReturnArguments);
			ProxyCallCheckReturnType(resp.ReturnValue, method.ReturnType);
			return resp.ReturnValue;
		}
		private Boolean ProxyCanCastTo(IProxyBase obj, Type type) {
			return (Boolean)SyncCall(new ObjectCanCastToRequest() { Object = obj, Type = type });
		}
		private void ProxyReleaseObject(ProxyObjectReference objref) {
			if (objref == null) return;
			ProxyReleaseObject(objref, objref.RefCnt);
		}
		private void ProxyReleaseObject(ProxyObjectReference objref, int refcnt) {
			lock (ObjectReferencesByID) {
				int newrefcnt = objref.Release(refcnt);
				IObjectReferenceBase regobjref;
				if (newrefcnt <= 0 && ObjectReferencesByID.TryGetValue(objref.ID, out regobjref) && objref == regobjref) ObjectReferencesByID.Remove(objref.ID);
			}
			try {
				if (Closed) return;
				SendObject(new ReferenceReleaseRequest() { ObjectID = objref.RemoteID, ReferenceCount = refcnt });
			} catch (Exception) { } //Assume the exception happened because the connection is closed. Otherwise memory leak...
		}

		class PendingRemoteCall {
			public UInt32 ID = 0;
			public ManualResetEvent WaitHandle = null;
			public Object Response = null;
			public Exception Error = null;
			public Boolean Completed = false;
			public SynchronousCall? NextCall = null;
			public Object CallData = null;

			public void SetError(Exception error) {
				this.Error = error;
				Completed = true;
				if (WaitHandle != null) WaitHandle.Set();
			}
			public void SetResult(Object result) {
				this.Response = result;
				Completed = true;
				if (WaitHandle != null) WaitHandle.Set();
			}
			public Boolean SetNextCall(SynchronousCall nextcall) {
				if (WaitHandle == null) return false;
				this.NextCall = nextcall;
				WaitHandle.Set();
				return true;
			}
		}
		#endregion

		#region Object serialization support
		private void Serialize(Stream stream, Object obj) {
			BinaryWriter writer = new BinaryWriter(stream);
			Serialize(writer, obj);
			writer.Flush();
		}
		private void Serialize(BinaryWriter writer, Object obj) {
			if (ReferenceEquals(obj, null)) {
				writer.Write((Byte)0);
			} else if (GetRealProxyForObject(obj) != null) {
				GetObjectData(obj, writer);
			} else {
				Type type = obj.GetType();
				if (type == typeof(Boolean)) {
					writer.Write((Byte)1);
					writer.Write((Boolean)obj);
				} else if (type == typeof(Byte)) {
					writer.Write((Byte)2);
					writer.Write((Byte)obj);
				} else if (type == typeof(Char)) {
					writer.Write((Byte)3);
					writer.Write((Int16)obj);
				} else if (type == typeof(Decimal)) {
					writer.Write((Byte)4);
					writer.Write((Decimal)obj);
				} else if (type == typeof(Double)) {
					writer.Write((Byte)5);
					writer.Write((Double)obj);
				} else if (type == typeof(Int16)) {
					writer.Write((Byte)6);
					writer.Write((Int16)obj);
				} else if (type == typeof(Int32)) {
					writer.Write((Byte)7);
					writer.Write((Int32)obj);
				} else if (type == typeof(Int64)) {
					writer.Write((Byte)8);
					writer.Write((Int64)obj);
				} else if (type == typeof(SByte)) {
					writer.Write((Byte)9);
					writer.Write((SByte)obj);
				} else if (type == typeof(Single)) {
					writer.Write((Byte)10);
					writer.Write((Single)obj);
				} else if (type == typeof(String)) {
					writer.Write((Byte)11);
					writer.Write((String)obj);
				} else if (type == typeof(UInt16)) {
					writer.Write((Byte)12);
					writer.Write((UInt16)obj);
				} else if (type == typeof(UInt32)) {
					writer.Write((Byte)13);
					writer.Write((UInt32)obj);
				} else if (type == typeof(UInt64)) {
					writer.Write((Byte)14);
					writer.Write((UInt64)obj);
				} else if (type == typeof(DateTime)) {
					writer.Write((Byte)32);
					writer.Write((Int64)((DateTime)obj).ToBinary());
				} else if (type.IsSubclassOf(typeof(Type))) {
					writer.Write((Byte)165);
					SerializeType(writer, (Type)obj);
				} else if (type.IsArray) {
					writer.Write((Byte)164);
					Array arr = (Array)obj;
					writer.Write((int)arr.Length);
					SerializeType(writer, type.GetElementType());
					for (int i = 0; i < arr.Length; i++) {
						Serialize(writer, arr.GetValue(i));
					}
				} else if (type.IsPrimitive) {
					throw new NotSupportedException();
				} else if (typeof(Delegate).IsAssignableFrom(type) || type.IsMarshalByRef) {
					GetObjectData(obj, writer);
				} else if (obj is ISerializable) {
					SerializationInfo si = new SerializationInfo(type, new TypeFixerConverter());
					((ISerializable)obj).GetObjectData(si, new StreamingContext(StreamingContextStates.All));
					writer.Write((Byte)128);
					SerializeType(writer, Type.GetType(si.FullTypeName + "," + si.AssemblyName));
					writer.Write((int)si.MemberCount);
					foreach (SerializationEntry se in si) {
						writer.Write(se.Name);
						Serialize(writer, se.Value);
					}
				} else if (type.IsSerializable) {
					MemberInfo[] members = FormatterServices.GetSerializableMembers(type);
					Object[] values = FormatterServices.GetObjectData(obj, members);
					writer.Write((Byte)128);
					SerializeType(writer, type);
					writer.Write((int)members.Length);
					for (int i = 0; i < members.Length; i++) {
						writer.Write(members[i].Name);
						Serialize(writer, values[i]);
					}
				} else {
					GetObjectData(obj, writer);
				}
			}
		}
		private void SerializeType(BinaryWriter writer, Type t) {
			writer.Write(t.FullName);
			writer.Write(t.Assembly.FullName);
		}
		private Object Deserialize(Stream stream) {
			BinaryReader reader = new BinaryReader(stream);
			List<IDeserializationCallback> callbackobjects = new List<IDeserializationCallback>();
			Object obj = Deserialize(reader, callbackobjects);
			foreach (IDeserializationCallback cb in callbackobjects) cb.OnDeserialization(this);
			return obj;
		}
		private Object Deserialize(BinaryReader reader, List<IDeserializationCallback> callbackobjects) {
			Byte t = reader.ReadByte();
			if (t == 0) return null;
			if (t == 1) return reader.ReadBoolean();
			if (t == 2) return reader.ReadByte();
			if (t == 3) return (Char)reader.ReadInt16();
			if (t == 4) return reader.ReadDecimal();
			if (t == 5) return reader.ReadDouble();
			if (t == 6) return reader.ReadInt16();
			if (t == 7) return reader.ReadInt32();
			if (t == 8) return reader.ReadInt64();
			if (t == 9) return reader.ReadSByte();
			if (t == 10) return reader.ReadSingle();
			if (t == 11) return reader.ReadString();
			if (t == 12) return reader.ReadUInt16();
			if (t == 13) return reader.ReadUInt32();
			if (t == 14) return reader.ReadUInt64();
			if (t == 32) return DateTime.FromBinary(reader.ReadInt64());
			if (t == 128) {
				Type type = DeserializeType(reader);
				int cnt = reader.ReadInt32();
				Object inst;
				StreamingContext sc = new StreamingContext(StreamingContextStates.All);
				if (type == null) {
					Dictionary<String, Object> obj = new Dictionary<String, Object>();
					for (int i = 0; i < cnt; i++) {
						String name = reader.ReadString();
						Object value = Deserialize(reader, callbackobjects);
						obj.Add(name, value);
					}
					return obj;
				} else if (typeof(ISerializable).IsAssignableFrom(type) && type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(SerializationInfo), typeof(StreamingContext) }, null) != null) {
					SerializationInfo si = new SerializationInfo(type, new TypeFixerConverter());
					for (int i = 0; i < cnt; i++) {
						String name = reader.ReadString();
						Object value = Deserialize(reader, callbackobjects);
						si.AddValue(name, value);
					}
					inst = Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Object[] { si, sc }, null);
				} else if (type.IsSerializable) {
					inst = FormatterServices.GetUninitializedObject(type);
					List<MemberInfo> members = new List<MemberInfo>();
					List<Object> values = new List<object>();
					for (int i = 0; i < cnt; i++) {
						String mname = reader.ReadString();
						Object value = Deserialize(reader, callbackobjects);
						MemberInfo[] mms = type.GetMember(mname, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (mms.Length != 1) throw new InvalidOperationException();
						if (mms[0] is PropertyInfo) {
							value = FixObjectType(value, ((PropertyInfo)mms[0]).PropertyType);
						} else if (mms[0] is FieldInfo) {
							value = FixObjectType(value, ((FieldInfo)mms[0]).FieldType);
						}
						members.Add(mms[0]);
						values.Add(value);
					}
					FormatterServices.PopulateObjectMembers(inst, members.ToArray(), values.ToArray());
				} else {
					throw new InvalidOperationException("Type " + type.Name + " is not serializable");
				}
				IObjectReference objref = inst as IObjectReference;
				if (objref != null) inst = objref.GetRealObject(sc);
				IDeserializationCallback deserializationcallback = inst as IDeserializationCallback;
				if (deserializationcallback != null && callbackobjects != null) callbackobjects.Add(deserializationcallback);
				return inst;
			}
			if (t == 129 || t == 130 || t == 131 || t == 132) {
				return SetObjectData(t, reader);
			}
			if (t == 164) {
				int len = reader.ReadInt32();
				Type type = DeserializeType(reader);
				if (type == null) type = typeof(Object);
				Array arr = Array.CreateInstance(type, len);
				for (int i = 0; i < len; i++) arr.SetValue(Deserialize(reader, callbackobjects), i);
				return arr;
			}
			if (t == 165) {
				return DeserializeType(reader);
			}
			throw new NotSupportedException();
		}
		private Type DeserializeType(BinaryReader reader) {
			String name = reader.ReadString();
			String aname = reader.ReadString();
			if (name.Length == 0 && aname.Length == 0) return null;
			Type t = Type.GetType(name + ", " + aname, false);
			if (t != null) return t;
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				t = assembly.GetType(name, false);
				if (t != null) return t;
			}
			return t;
		}

		Dictionary<UInt32, IObjectReferenceBase> ObjectReferencesByID = new Dictionary<uint, IObjectReferenceBase>();
		List<RemoteObjectReference> RemoteObjectReferences = new List<RemoteObjectReference>();
		uint remoteReferenceIndex = 0;
		void GetObjectData(object obj, BinaryWriter w) {
			IObjectReferenceBase rref = null;
			IProxyBase proxy = GetRealProxyForObject(obj);
			if (proxy != null && proxy.Manager == this) {
				rref = proxy.ObjectReference;
				w.Write((Byte)131);
				w.Write((UInt32)rref.RemoteID);
			} else {
				Boolean isDelegate = obj is Delegate;
				lock (ObjectReferencesByID) {
					foreach (IObjectReferenceBase objref in RemoteObjectReferences) {
						Object other = objref.Target;
						if (ReferenceEquals(other, obj) || (isDelegate && Delegate.Equals(other, obj))) {
							rref = objref;
							break;
						}
					}
					if (rref == null) {
						UInt32 objid;
						lock (ObjectReferencesByID) {
							while (true) {
								remoteReferenceIndex++;
								objid = remoteReferenceIndex;
								if ((objid & 0x80000000) != 0) {
									remoteReferenceIndex = 0;
									continue;
								}
								if (!ObjectReferencesByID.ContainsKey(objid)) break;
								Monitor.Wait(ObjectReferencesByID);
							}
						}
						rref = new RemoteObjectReference(objid, obj);
						ObjectReferencesByID.Add(objid, rref);
						RemoteObjectReferences.Add((RemoteObjectReference)rref);
					}
					rref.AddRef();
				}
				if (isDelegate) {
					w.Write((Byte)130);
					w.Write((UInt32)rref.RemoteID);
					SerializeType(w, obj.GetType());
				} else if (obj is IRemotableWithCachedProperties) {
					SerializationInfo si = new SerializationInfo(obj.GetType(), new TypeFixerConverter());
					((IRemotableWithCachedProperties)obj).GetObjectData(si);
					w.Write((Byte)132);
					w.Write((UInt32)rref.RemoteID);
					w.Write((int)si.MemberCount);
					foreach (SerializationEntry se in si) {
						w.Write(se.Name);
						Serialize(w, se.Value);
					}
				} else {
					w.Write((Byte)129);
					w.Write((UInt32)rref.RemoteID);
				}
			}
		}
		object SetObjectData(Byte t, BinaryReader r) {
			UInt32 objid = r.ReadUInt32();
			Type deltype = null;
			if (t == 130) deltype = DeserializeType(r);
			if (t == 132) {
				int cnt = r.ReadInt32();
				for (int i = 0; i < cnt; i++) {
					r.ReadString();
					Deserialize(r, null);
				}
			}
			lock (ObjectReferencesByID) {
				Object target = null;
				IObjectReferenceBase rref;
				if (ObjectReferencesByID.TryGetValue(objid, out rref)) target = rref.Target;
				if (t == 131) {
					if (target == null) throw new InvalidOperationException("Object not found");
				} else {
					if (target == null) {
						if ((objid & 0x80000000) == 0) throw new InvalidOperationException("The Object ID is invalid");
						IProxyBase proxy;
						if (t == 130) {
							proxy = CreateDelegateProxy(objid, deltype);
						} else {
							String[] iftypes = null; //(String[])info.GetValue("InterfaceTypes", typeof(String[]));
							proxy = CreateObjectProxy(objid, iftypes);
						}
						rref = proxy.ObjectReference;
						ObjectReferencesByID[objid] = rref;
						target = proxy.GetTransparentProxy();
					}
					rref.AddRef();
				}
				return target;
			}
		}
		#endregion

		#region RPC messages
		[Serializable]
		struct SynchronousCall {
			public UInt32 CallID;
			public Boolean IsResult;
			public Object Data;
			public Boolean HasPreviousCallID;
			public UInt32 PreviousCallID;
		}

		[Serializable]
		struct MethodCallRequest {
			public Object Object;
			public Type Type;
			public String MethodName;
			public Type[] MethodSignature;
			public Object[] Arguments;
		}
		[Serializable]
		struct MethodCallResponse {
			public Exception Exception;
			public Object ReturnValue;
			public Object[] ReturnArguments;
		}
		[Serializable]
		struct PropertyAccessRequest {
			public Object Object;
			public Type Type;
			public String PropertyName;
			public Boolean SetValue;
			public Object Value;
		}
		[Serializable]
		struct ReferenceReleaseRequest {
			public UInt32 ObjectID;
			public Int32 ReferenceCount;
		}
		[Serializable]
		struct GetRootRequest {
		}
		[Serializable]
		struct ObjectCanCastToRequest {
			public Object Object;
			public Type Type;
		}
		[Serializable]
		struct CreateStreamRequest {
			public StreamChannel StreamObject;
			public int StreamID;
		}
		[Serializable]
		struct DelegateCallRequest {
			public Object Delegate;
			public Object[] Arguments;
		}
		[Serializable]
		struct EchoRequest {
			public Object Object;
		}
		#endregion

		#region Proxy magic
		static IProxyBase GetRealProxyForObject(Object obj) {
			if (RemotingServices.IsTransparentProxy(obj)) return RemotingServices.GetRealProxy(obj) as FWProxy;
			IProxyBase obj_IProxyBase = obj as IProxyBase;
			if (obj_IProxyBase != null) return obj_IProxyBase;
			Delegate obj_Delegate = obj as Delegate;
			if (obj_Delegate != null) {
				DelegateProxy pb = obj_Delegate.Target as DelegateProxy;
				if (pb != null) return pb;
			}
			return null;
		}
		public static RemotingManager GetManagerForObjectProxy(Object obj) {
			IProxyBase prox = GetRealProxyForObject(obj);
			if (prox == null) return null;
			return prox.Manager;
		}

		interface IObjectReferenceBase {
			UInt32 ID { get; }
			Object Target { get; }
			int AddRef();
			int RefCnt { get; }
			Boolean IsLocal { get; }
			UInt32 RemoteID { get; }
		}

		class RemoteObjectReference : IObjectReferenceBase {
			int refcnt = 0;
			public UInt32 ID { get; private set; }
			public Object Target { get; private set; }
			public int RefCnt { get { return refcnt; } }
			public int AddRef() { return Interlocked.Increment(ref refcnt); }
			public int Release(int count) { return Interlocked.Add(ref refcnt, -count); }
			public Boolean IsLocal { get { return true; } }
			public UInt32 RemoteID { get { return ID | 0x80000000; } }
			public RemoteObjectReference(UInt32 id, Object obj) {
				if ((id & 0x80000000) != 0) throw new InvalidOperationException("The Object ID is invalid");
				this.ID = id;
				this.Target = obj;
			}
		}

		interface IProxyBase {
			RemotingManager Manager { get; }
			UInt32 ID { get; }
			ProxyObjectReference ObjectReference { get; }
			Object GetTransparentProxy();
		}
		class ProxyObjectReference : IObjectReferenceBase {
			int refcnt = 0;
			WeakReference targetref;
			public UInt32 ID { get; private set; }
			public RemotingManager Manager { get; private set; }
			public Object Target {
				get {
					IProxyBase proxy = this.Proxy;
					if (proxy == null) return null;
					return proxy.GetTransparentProxy();
				}
			}
			public IProxyBase Proxy {
				get {
					if (targetref == null) return null;
					return (IProxyBase)targetref.Target; 
				} 
			}
			public int RefCnt { get { return refcnt; } }
			public int AddRef() {
				int newcnt = Interlocked.Increment(ref refcnt);
				if (newcnt > 10000 && Interlocked.CompareExchange(ref refcnt, 1, newcnt) == newcnt) {
					Manager.ProxyReleaseObject(this, newcnt - 1);
					newcnt = 1;
				}
				return newcnt;
			}
			public int Release(int count) { return Interlocked.Add(ref refcnt, -count); }
			public Boolean IsLocal { get { return false; } }
			public UInt32 RemoteID { get { return ID & 0x7FFFFFFF; } }
			public ProxyObjectReference(IProxyBase proxy) {
				this.Manager = proxy.Manager;
				this.ID = proxy.ID;
				if ((ID & 0x80000000) == 0) throw new InvalidOperationException("The Object ID is invalid");
				targetref = new WeakReference(proxy);
			}
		}
		class DelegateProxy : IProxyBase {
			public ProxyObjectReference ObjectReference { get; private set; }
			public RemotingManager Manager { get; private set; }
			public UInt32 ID { get; private set; }
			public Delegate Target { get; private set; }
			public MethodInfo MethodSignature { get; private set; }
			public Object GetTransparentProxy() { return Target; }

			public DelegateProxy(RemotingManager manager, UInt32 objid, MethodInfo methodinfo, Type delegateType, DynamicMethod methodBuilder) {
				this.Manager = manager;
				this.ID = objid;
				this.MethodSignature = methodinfo;
				Delegate mi = methodBuilder.CreateDelegate(delegateType, this);
				ObjectReference = new ProxyObjectReference(this);
			}
			~DelegateProxy() {
				Manager.ProxyReleaseObject(ObjectReference);
			}
			private Object DoCall(Object[] args) {
				return Manager.ProxyMakeCallDelegate(this, args);
			}
		}
		class FWProxy : RealProxy, IRemotingTypeInfo, IProxyBase {
			public RemotingManager Manager { get; private set; }
			public UInt32 ID { get; private set; }
			public ProxyObjectReference ObjectReference { get; private set; }

			public FWProxy(RemotingManager manager, UInt32 objid) : base(typeof(MarshalByRefObject)) {
				this.Manager = manager;
				this.ID = objid;
				this.ObjectReference = new ProxyObjectReference(this);
			}

			~FWProxy() {
				Manager.ProxyReleaseObject(ObjectReference);
			}

			public string TypeName { get; set; }

			public override IMessage Invoke(IMessage msg) {
				IMethodCallMessage methodCallMessage = msg as IMethodCallMessage;
				if (methodCallMessage != null) {
					Object r = Manager.ProxyMakeCall(this, (MethodInfo)methodCallMessage.MethodBase, methodCallMessage.Args);
					return new ReturnMessage(r, null, 0, null, methodCallMessage);
				}
				throw new NotImplementedException();
			}

			public bool CanCastTo(Type fromType, object o) {
				if (fromType == typeof(ISerializable)) return false;
				return Manager.ProxyCanCastTo(this, fromType);
			}
		}
		class ProxyBase : IProxyBase {
			public RemotingManager Manager { get; private set; }
			public UInt32 ID { get; private set; }
			public ProxyObjectReference ObjectReference { get; private set; }
			public Object GetTransparentProxy() { return this; }

			public void Init(RemotingManager manager, UInt32 objid) {
				this.Manager = manager;
				this.ID = objid;
				this.ObjectReference = new ProxyObjectReference(this);
			}

			protected Object DoCall(RuntimeMethodHandle methodh, Object[] args) {
				MethodInfo meth = (MethodInfo)MethodInfo.GetMethodFromHandle(methodh);
				return Manager.ProxyMakeCall(this, meth, args);
			}

			~ProxyBase() {
				Manager.ProxyReleaseObject(ObjectReference);
			}
		}

		private IProxyBase CreateDelegateProxy(UInt32 objid, Type deltype) {
			MethodInfo newMethod = deltype.GetMethod("Invoke");
			ParameterInfo[] parameters = newMethod.GetParameters();
			Type[] mparams = ArrayUtil.Merge(new Type[1] { typeof(DelegateProxy) }, Array.ConvertAll(parameters, delegate(ParameterInfo pi) { return pi.ParameterType; }));
			DynamicMethod methodBuilder = new DynamicMethod(String.Empty, newMethod.ReturnType, mparams, typeof(DelegateProxy));
			ILGenerator ilGenerator = methodBuilder.GetILGenerator();
			GenerateProxyMethodCode(ilGenerator, parameters, newMethod.ReturnType, typeof(DelegateProxy), "DoCall", null);
			return new DelegateProxy(this, objid, newMethod, deltype, methodBuilder);
		}

		private IProxyBase CreateObjectProxy(UInt32 objid, String[] typeNames) {
			DebugLog("Create proxy for remote object {0}", objid);
			IProxyBase proxy;
			if (true) {
				proxy = new FWProxy(this, objid);
			} else {
				Type[] types = new Type[typeNames.Length];
				int j = 0;
				for (int i = 0; i < typeNames.Length; i++) {
					Type t = Type.GetType(typeNames[i], false);
					if (t == null || !t.IsInterface) continue;
					types[j] = t;
					j++;
				}
				Array.Resize(ref types, j);
				Type proxyType = GetProxyType(types);
				proxy = (ProxyBase)Activator.CreateInstance(proxyType);
				((ProxyBase)proxy).Init(this, objid);
			}
			return proxy;
		}

		private static ModuleBuilder moduleBuilder = null;
		private static Dictionary<String, Type> proxyCache = null;
		private static Type GetProxyType(Type[] interfaceTypes) {
			Type proxyType;
			String key = String.Join("&", Array.ConvertAll(interfaceTypes, delegate(Type t) { return t.Name; }));
			lock (typeof(RemotingManager)) if (proxyCache == null) proxyCache = new Dictionary<String, Type>();
			lock (proxyCache) {
				if (!proxyCache.TryGetValue(key, out proxyType)) {
					proxyType = GenerateProxyType(key, interfaceTypes);
					proxyCache.Add(key, proxyType);
				}
			}
			return proxyType;
		}
		private static Type GenerateProxyType(String name, Type[] interfaceTypes) {
			lock (typeof(RemotingManager)) {
				if (moduleBuilder == null) {
					AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("UCIS.Remoting.Proxies"), AssemblyBuilderAccess.Run);
					moduleBuilder = assembly.DefineDynamicModule("UCIS.Remoting.Proxies", false);
				}
			}
			TypeBuilder typeBuilder = moduleBuilder.DefineType(
				name.Length == 0 ? "UndefinedProxy" : name, //mono does not like types with no name!
				TypeAttributes.NotPublic | TypeAttributes.Sealed,
				typeof(ProxyBase),
				interfaceTypes);
			foreach (Type interfaceType in interfaceTypes) {
				foreach (MethodInfo method in interfaceType.GetMethods()) {
					GenerateProxyMethod(typeBuilder, method);
				}
			}
			return typeBuilder.CreateType();
		}
		private static void GenerateProxyMethod(TypeBuilder typeBuilder, MethodInfo newMethod) {
			if (newMethod.IsGenericMethod) newMethod = newMethod.GetGenericMethodDefinition();
			ParameterInfo[] parameters = newMethod.GetParameters();
			Type[] parameterTypes = Array.ConvertAll(parameters, delegate(ParameterInfo parameter) { return parameter.ParameterType; });

			MethodBuilder methodBuilder = typeBuilder.DefineMethod(
				"Impl_" + newMethod.DeclaringType.Name + "_" + newMethod.Name,
				MethodAttributes.Public | MethodAttributes.Virtual,
				newMethod.ReturnType,
				parameterTypes);
			typeBuilder.DefineMethodOverride(methodBuilder, newMethod);

			if (newMethod.IsGenericMethod) {
				methodBuilder.DefineGenericParameters(Array.ConvertAll(newMethod.GetGenericArguments(), delegate(Type type) { return type.Name; }));
			}

			ILGenerator ilGenerator = methodBuilder.GetILGenerator();
			GenerateProxyMethodCode(ilGenerator, parameters, newMethod.ReturnType, typeof(ProxyBase), "DoCall", newMethod);
		}
		private static void GenerateProxyMethodCode(ILGenerator ilGenerator, ParameterInfo[] parameters, Type returnType, Type baseType, String baseMethod, MethodInfo methodRef) {
			LocalBuilder localBuilder = ilGenerator.DeclareLocal(typeof(Object[]));
			ilGenerator.Emit(OpCodes.Ldc_I4, parameters.Length);
			ilGenerator.Emit(OpCodes.Newarr, typeof(Object));
			ilGenerator.Emit(OpCodes.Stloc, localBuilder);
			for (int i = 0; i < parameters.Length; i++) {
				if (parameters[i].ParameterType.IsByRef) continue;
				ilGenerator.Emit(OpCodes.Ldloc, localBuilder);
				ilGenerator.Emit(OpCodes.Ldc_I4, i);
				ilGenerator.Emit(OpCodes.Ldarg, i + 1);
				if (parameters[i].ParameterType.IsValueType) ilGenerator.Emit(OpCodes.Box, parameters[i].ParameterType);
				ilGenerator.Emit(OpCodes.Stelem_Ref);
			}
			ilGenerator.Emit(OpCodes.Ldarg_0);
			if (methodRef != null) ilGenerator.Emit(OpCodes.Ldtoken, methodRef);
			ilGenerator.Emit(OpCodes.Ldloc, localBuilder);
			ilGenerator.Emit(OpCodes.Call, baseType.GetMethod(baseMethod, BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.NonPublic));
			if (returnType == typeof(void)) {
				ilGenerator.Emit(OpCodes.Pop);
			} else if (returnType.IsValueType) {
				ilGenerator.Emit(OpCodes.Unbox_Any, returnType);
			}
			ilGenerator.Emit(OpCodes.Ret);
		}
		#endregion
	}

	public interface IRemotableWithCachedProperties {
		void GetObjectData(SerializationInfo info);
	}
}
