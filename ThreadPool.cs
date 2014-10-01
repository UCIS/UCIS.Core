using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace UCIS {
	public class ThreadPool {
		private static ThreadPool pManager = null;

		public static ThreadPool DefaultPool {
			get {
				if (pManager == null) pManager = new ThreadPool();
				return pManager;
			}
		}

		//Starts a long-term background task
		public static WorkItem RunTask(WaitCallback Callback, object State) {
			return DefaultPool.QueueWorkItem(Callback, State);
		}
		//Starts a short-term background task
		public static WorkItem RunCall(WaitCallback Callback, object State) {
			return DefaultPool.QueueWorkItem(Callback, State);
		}


		public class WorkItem {
			public WaitCallback Callback { get; internal set; }
			public object State { get; internal set; }
			public ThreadInfo Thread { get; internal set; }
		}
		public class ThreadInfo {
			public Thread Thread { get; internal set; }
			internal AutoResetEvent WaitHandle = new AutoResetEvent(false);
			public WorkItem WorkItem { get; internal set; }
			public bool Busy { get; internal set; }
			public bool Abort { get; internal set; }
			public DateTime LastActive { get; internal set; }
		}

		public class ExceptionEventArgs : EventArgs {
			public ExceptionEventArgs(WorkItem Item, Exception Exception, bool ThrowError) {
				this.Item = Item;
				this.Exception = Exception;
				this.ThrowError = ThrowError;
			}
			public WorkItem Item;
			public Exception Exception;
			public bool ThrowError;
		}

		private List<ThreadInfo> pThreads = new List<ThreadInfo>();
		private int pBusyThreads = 0;
		private Queue<ThreadInfo> pIdleThreads = new Queue<ThreadInfo>();
		private int pThreadsMax;
		private int pThreadsMinIdle;
		private int pThreadsMaxIdle;

		public event OnExceptionEventHandler OnException;
		public delegate void OnExceptionEventHandler(ThreadPool sender, ExceptionEventArgs e);

		public ReadOnlyCollection<ThreadInfo> Threads { get { return pThreads.AsReadOnly(); } }

		public ThreadPool() : this(250, 0, 5) { }

		public ThreadPool(int MaxThreads, int MinIdle, int MaxIdle) {
			if (MaxThreads < 0) {
				throw new ArgumentOutOfRangeException("ThreadsMaxIdle", "ThreadsMaxIdle must greater than 0");
			} else if (MaxThreads < MaxIdle) {
				throw new ArgumentOutOfRangeException("ThreadsMax", "ThreadsMax must be greater than or equal to ThreadsMaxIdle");
			} else if (MaxIdle < 0) {
				throw new ArgumentOutOfRangeException("ThreadsMaxIdle", "ThreadsMaxIdle must greater than or equal to 0");
			} else if (MinIdle < 0) {
				throw new ArgumentOutOfRangeException("ThreadsMinIdle", "ThreadsMinIdle must greater than or equal to 0");
			} else if (MinIdle > MaxIdle) {
				throw new ArgumentOutOfRangeException("ThreadsMaxIdle", "ThreadsMaxIdle must be greater than or equal to ThreadsMinIdle");
			}
			pThreadsMax = MaxThreads;
			pThreadsMinIdle = MinIdle;
			pThreadsMaxIdle = MaxIdle;
			for (int I = 1; I <= pThreadsMinIdle; I++) {
				StartThread(false);
			}
		}

		public int ThreadsIdle { get { return pIdleThreads.Count; } }
		public int ThreadsBusy { get { return pBusyThreads; } }
		public int ThreadsAlive { get { return pThreads.Count; } }
		public int ThreadsMinIdle {
			get { return pThreadsMinIdle; }
			set {
				if (value > pThreadsMaxIdle) {
					throw new ArgumentOutOfRangeException("ThreadsMinIdle", "ThreadsMinIdle must be smaller than ThreadsMaxIdle");
				} else if (value < 0) {
					throw new ArgumentOutOfRangeException("ThreadsMinIdle", "ThreadsMinIdle must greater than or equal to 0");
				} else {
					int I = 0;
					int C = 0;
					C = pIdleThreads.Count;
					if (value > C) {
						for (I = C; I <= value - 1; I++) {
							StartThread(false);
						}
					}
					pThreadsMinIdle = value;
				}
			}
		}
		public int ThreadsMaxIdle {
			get { return pThreadsMaxIdle; }
			set {
				if (pThreadsMinIdle > value) throw new ArgumentOutOfRangeException("ThreadsMaxIdle", "ThreadsMaxIdle must be greater than or equal to ThreadsMinIdle");
				if (value < 0) throw new ArgumentOutOfRangeException("ThreadsMaxIdle", "ThreadsMaxIdle must greater than or equal to 0");
				lock (pIdleThreads) {
					while (value < pIdleThreads.Count) {
						ThreadInfo T = pIdleThreads.Dequeue();
						T.Abort = true;
						T.WaitHandle.Set();
					}
				}
				pThreadsMaxIdle = value;
			}
		}
		public int ThreadsMax {
			get { return pThreadsMax; }
			set {
				if (pThreadsMaxIdle > value) throw new ArgumentOutOfRangeException("ThreadsMax", "ThreadsMax must be greater than or equal to ThreadsMaxIdle");
				if (value <= 0) throw new ArgumentOutOfRangeException("ThreadsMax", "ThreadsMax must greater than 0");
				pThreadsMax = value;
			}
		}

		public WorkItem QueueWorkItem(WaitCallback Callback, object State) {
			WorkItem WorkItem = new WorkItem() { Callback = Callback, State = State };
			ThreadInfo Thread = null;
			lock (pIdleThreads) {
				while (Thread == null && pIdleThreads.Count > 0) {
					Thread = pIdleThreads.Dequeue();
					if (Thread.Abort) Thread = null;
				}
			}
			if (Thread == null)  {
				if (pThreads.Count >= pThreadsMax) throw new ThreadStateException("Thread limit exceeded");
				Thread = StartThread(true);
			}
			Thread.LastActive = DateTime.Now;
			WorkItem.Thread = Thread;
			Thread.WorkItem = WorkItem;
			Thread.WaitHandle.Set();
			return WorkItem;
		}

		private ThreadInfo StartThread(bool Reserved) {
			ThreadInfo Thread = new ThreadInfo();
			Thread.Thread = new Thread(pWorker);
			lock (pThreads) {
				pThreads.Add(Thread);
				if (!Reserved) pIdleThreads.Enqueue(Thread);
			}
			Thread.LastActive = DateTime.Now;
			Thread.Thread.Start(Thread);
			return Thread;
		}

		public void AbortAllThreads() {
			lock (pIdleThreads) {
				while (pIdleThreads.Count > 0) {
					ThreadInfo Thread = pIdleThreads.Dequeue();
					Thread.Abort = true;
					Thread.WaitHandle.Set();
				}
			}
			foreach (ThreadInfo Thread in pThreads.ToArray()) {
				if (Thread != null && !Thread.Abort) {
					Thread.Thread.Abort();
					Thread.Abort = true;
					Thread.WaitHandle.Set();
				}
			}
			pIdleThreads.Clear();
		}

		//ToDo: add timer to kill old threads periodically
		public void KillOldThreads() {
			ThreadInfo Thread;
			lock (pIdleThreads) {
				if (pIdleThreads.Count == 0) return;
				Thread = pIdleThreads.Dequeue();
			}
			if (DateTime.Now.Subtract(Thread.LastActive).TotalMinutes > 1) {
				Thread.Abort = true;
				Thread.WaitHandle.Set();
			} else {
				lock (pIdleThreads) pIdleThreads.Enqueue(Thread);
			}
		}

		private void pWorker(object state) {
			ThreadInfo Thread = (ThreadInfo)state;
			if (Thread == null) throw new ArgumentNullException("state");
			try {
				while (true) {
					if (Thread.WaitHandle == null) throw new ArgumentNullException("WaitHandle");
					if (!Thread.WaitHandle.WaitOne(1000, false)) {
						if (pBusyThreads <= 0) return;
						continue;
					}
					if (Thread.Abort) break;

					Thread.Busy = true;
					Interlocked.Increment(ref pBusyThreads);
					try {
						if (Thread.WorkItem == null) throw new ArgumentNullException("WorkItem");
						if (Thread.WorkItem.Callback == null) throw new ArgumentNullException("WorkItem.Callback");
						Thread.WorkItem.Callback.Invoke(Thread.WorkItem.State);
					} catch (ThreadAbortException ex) {
						ExceptionEventArgs e = new ExceptionEventArgs(Thread.WorkItem, ex, false);
						if (OnException != null) OnException(this, e);
						if (e.ThrowError) Console.WriteLine("ThreadAbortException in ThreadPool thread: " + e.Exception.ToString());
						return;
					} catch (Exception ex) {
						ExceptionEventArgs e = new ExceptionEventArgs(Thread.WorkItem, ex, true);
						if (OnException != null) OnException(this, e);
						if (e.ThrowError) {
							Console.WriteLine("Exception in ThreadPool thread: " + e.Exception.ToString());
							throw new Exception("Unhandled exception in work item", e.Exception);
						}
					} finally {
						Interlocked.Decrement(ref pBusyThreads);
					}
					Thread.WorkItem.Thread = null;
					Thread.WorkItem = null;
					Thread.Busy = false;
					lock (pIdleThreads) {
						if (pIdleThreads.Count >= pThreadsMaxIdle) break;
						pIdleThreads.Enqueue(Thread);
					}
				}
			} finally {
				Thread.Abort = true;
				lock (pThreads) pThreads.Remove(Thread);
			}
		}
	}
}
