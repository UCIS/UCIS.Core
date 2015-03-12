using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using UCIS.Util;

namespace UCIS {
	public class ThreadPool {
		static readonly ThreadPool pManager = new ThreadPool();
		public static ThreadPool DefaultPool { get { return pManager; } }

		public static void RunTask(WaitCallback Callback, object State) {
			DefaultPool.QueueWorkItem(Callback, State);
		}
		public static void RunCall(WaitCallback Callback, object State) {
			DefaultPool.QueueWorkItem(Callback, State);
		}

		class WorkItem {
			public WaitCallback Callback { get; internal set; }
			public Object State { get; internal set; }
		}
		public class ExceptionEventArgs : EventArgs {
			public ExceptionEventArgs(Exception exception) {
				this.Exception = exception;
			}
			public Exception Exception { get; private set; }
		}

		WorkQueue<WorkItem> queue;

		public event OnExceptionEventHandler OnException;
		public delegate void OnExceptionEventHandler(ThreadPool sender, ExceptionEventArgs e);

		public ThreadPool() : this(250, 0, 5) { }

		public ThreadPool(int MaxThreads, int MinIdle, int MaxIdle) {
			queue = new WorkQueue<WorkItem>(handler);
			queue.UseFrameworkThreadpool = false;
			queue.MaxWorkers = MaxThreads;
			queue.MaxIdleWorkers = MaxIdle;
			queue.MinIdleWorkers = MinIdle;
		}

		public int ThreadsIdle { get { return queue.IdleWorkers; } }
		public int ThreadsBusy { get { return queue.TotalWorkers - queue.IdleWorkers; } }
		public int ThreadsAlive { get { return queue.TotalWorkers; } }
		public int ThreadsMax { get { return queue.MaxWorkers; } set { queue.MaxWorkers = value; } }
		public int ThreadsMaxIdle { get { return queue.MaxIdleWorkers; } set { queue.MaxIdleWorkers = value; } }
		public int ThreadsMinIdle { set { queue.MinIdleWorkers = value; } }

		public void QueueWorkItem(WaitCallback callback, Object state) {
			if (callback == null) throw new ArgumentNullException("callback");
			queue.Enqueue(new WorkItem() { Callback = callback, State = state });
		}

		void handler(WorkItem item) {
			try {
				item.Callback(item.State);
			} catch (Exception ex) {
				if (OnException != null) {
					OnException(this, new ExceptionEventArgs(ex));
				} else {
					throw;
				}
			}
		}
	}
}
